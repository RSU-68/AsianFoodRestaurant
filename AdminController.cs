using AsianFoodRestaurant.Data;
using AsianFoodRestaurant.Models;
using AsianFoodRestaurant.Services;
using AsianFoodRestaurant.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AsianFoodRestaurant.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly ISslCommerzService _sslCommerz;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, ISslCommerzService sslCommerz)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _sslCommerz = sslCommerz;
        }

        // ---------- Dashboard ----------
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.PendingOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
            ViewBag.TotalMenuItems = await _context.MenuItems.CountAsync();
            ViewBag.OutOfStock = await _context.MenuItems.CountAsync(m => m.StockQuantity == 0);
            ViewBag.TotalCustomers = (await _userManager.GetUsersInRoleAsync("Customer")).Count;
//------graph------
            var last7Days = Enumerable.Range(0, 7)
        .Select(i => DateTime.Today.AddDays(-i))
        .OrderBy(d => d)
        .ToList();

            var salesByDay = await _context.Orders
                .Where(o => o.OrderDate >= DateTime.Today.AddDays(-6)
                            && o.Payment != null && o.Payment.Status == PaymentStatus.Paid)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(o => o.TotalAmount) })
                .ToListAsync();

            ViewBag.SalesLabels = last7Days.Select(d => d.ToString("dd MMM")).ToList();
            ViewBag.SalesData = last7Days.Select(d => salesByDay.FirstOrDefault(s => s.Date == d)?.Total ?? 0).ToList();

            return View();
        }

        // ---------- Employees ----------
        public async Task<IActionResult> Employees()
        {
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            return View(employees.OrderBy(e => e.FullName).ToList());
        }

        [HttpGet]
        public IActionResult CreateEmployee() => View(new CreateEmployeeViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmployee(CreateEmployeeViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.Phone,
                EmployeeRole = model.EmployeeRole,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Employee");
                TempData["Success"] = $"Employee {user.FullName} created as {model.EmployeeRole}.";
                return RedirectToAction(nameof(Employees));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEmployeeRole(string id, EmployeeRole newRole)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            user.EmployeeRole = newRole;
            await _userManager.UpdateAsync(user);
            TempData["Success"] = $"{user.FullName}'s role updated to {newRole}.";
            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployee(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            await _userManager.DeleteAsync(user);
            TempData["Success"] = "Employee removed.";
            return RedirectToAction(nameof(Employees));
        }

        // ---------- Menu (Create / Edit / Delete / Image upload) ----------
        public async Task<IActionResult> Menu()
        {
            var items = await _context.MenuItems.OrderBy(m => m.Name).ToListAsync();
            return View(items);
        }

        [HttpGet]
        public IActionResult CreateMenuItem() => View(new MenuItemFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMenuItem(MenuItemFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var item = new MenuItem
            {
                Name = model.Name,
                Description = model.Description,
                Category = model.Category,
                Price = model.Price,
                StockQuantity = model.StockQuantity
            };

            item.ImagePath = await SaveImageAsync(model.ImageFile);

            _context.MenuItems.Add(item);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Menu item added.";
            return RedirectToAction(nameof(Menu));
        }

        [HttpGet]
        public async Task<IActionResult> EditMenuItem(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return NotFound();

            var model = new MenuItemFormViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Category = item.Category,
                Price = item.Price,
                StockQuantity = item.StockQuantity,
                ExistingImagePath = item.ImagePath
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMenuItem(MenuItemFormViewModel model)
        {
            var item = await _context.MenuItems.FindAsync(model.Id);
            if (item == null) return NotFound();

            if (!ModelState.IsValid) return View(model);

            item.Name = model.Name;
            item.Description = model.Description;
            item.Category = model.Category;
            item.Price = model.Price;
            item.StockQuantity = model.StockQuantity;

            if (model.ImageFile != null)
                item.ImagePath = await SaveImageAsync(model.ImageFile);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Menu item updated.";
            return RedirectToAction(nameof(Menu));
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return NotFound();

            try
            {
                _context.MenuItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Menu item deleted.";
            }
            catch (DbUpdateException)
            {
                
                _context.Entry(item).State = EntityState.Unchanged; // undo the pending delete
                item.StockQuantity = 0;
                await _context.SaveChangesAsync();
                TempData["Error"] = $"'{item.Name}' can't be deleted because it's part of one or more past orders. " +
                                     "It's been set to Stock Out (0) instead so customers can no longer order it.";
            }

            return RedirectToAction(nameof(Menu));
        }

        private async Task<string?> SaveImageAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "menu");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/menu/{fileName}";
        }

        // ---------- Stock management ----------
        public async Task<IActionResult> Stock()
        {
            var items = await _context.MenuItems.OrderBy(m => m.Name).ToListAsync();
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(int id, int stockQuantity)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return NotFound();

            item.StockQuantity = Math.Max(0, stockQuantity);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Stock for {item.Name} updated to {item.StockQuantity}.";
            return RedirectToAction(nameof(Stock));
        }

        // ---------- Customer account management ----------
        public async Task<IActionResult> Customers()
        {
            var customers = await _userManager.GetUsersInRoleAsync("Customer");
            return View(customers.OrderBy(c => c.FullName).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCustomerStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = $"{user.FullName} is now {(user.IsActive ? "Activated" : "Deactivated")}.";
            return RedirectToAction(nameof(Customers));
        }

        // ---------- Orders (Accept / Decline / Delete) ----------
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = OrderStatus.Accepted;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Order #{order.Id} accepted. Employees can now claim it.";
            return RedirectToAction(nameof(Orders));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = OrderStatus.Declined;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Order #{order.Id} declined.";
            return RedirectToAction(nameof(Orders));
        }

        

        
        // COD--
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCodPaid(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.Payment == null) return NotFound();

            if (order.Payment.Method != PaymentMethodType.COD)
            {
                TempData["Error"] = "This isn't a Cash on Delivery order.";
                return RedirectToAction(nameof(Orders));
            }

            order.Payment.Status = PaymentStatus.Paid;
            order.Payment.PaymentDate = DateTime.Now;
            order.Payment.TransactionId ??= $"COD-{DateTime.Now:yyyyMMddHHmmss}";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order #{order.Id} marked as paid (cash collected).";
            return RedirectToAction(nameof(Orders));
        }

        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            try
            {
                
                var relatedRatings = await _context.Ratings.Where(r => r.OrderId == id).ToListAsync();
                _context.Ratings.RemoveRange(relatedRatings);

                

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Order #{id} deleted.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = $"Order #{id} couldn't be deleted because other records still reference it.";
            }

            return RedirectToAction(nameof(Orders));
        }
        // ---------- Complaints ----------
        public async Task<IActionResult> Complaints()
        {
            var complaints = await _context.Complaints
                .Include(c => c.Customer)
                .Include(c => c.Order)
                .OrderByDescending(c => c.Status == ComplaintStatus.Open) 
                .ThenByDescending(c => c.CreatedDate)
                .ToListAsync();
            return View(complaints);
        }

        [HttpGet]
        public async Task<IActionResult> RespondComplaint(int id)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Customer)
                .Include(c => c.Order)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (complaint == null) return NotFound();

            var model = new RespondComplaintViewModel
            {
                Id = complaint.Id,
                AdminResponse = complaint.AdminResponse ?? "",
                Status = complaint.Status == ComplaintStatus.Open ? ComplaintStatus.InProgress : complaint.Status
            };

            ViewBag.Complaint = complaint;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondComplaint(RespondComplaintViewModel model)
        {
            var complaint = await _context.Complaints.FindAsync(model.Id);
            if (complaint == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Complaint = complaint;
                return View(model);
            }

            complaint.AdminResponse = model.AdminResponse;
            complaint.Status = model.Status;
            complaint.RespondedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Response sent for complaint #{complaint.Id}.";
            return RedirectToAction(nameof(Complaints));
        }

        // ---------- Ratings ----------
        public async Task<IActionResult> Ratings()
        {
            var ratings = await _context.Ratings
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            ViewBag.AverageRating = ratings.Any() ? ratings.Average(r => r.Stars) : 0;
            return View(ratings);
        }

        

        // ---------- Refunds ----------
        public async Task<IActionResult> Refunds()
        {
            var refunds = await _context.RefundRequests
                .Include(r => r.Customer)
                .Include(r => r.Order).ThenInclude(o => o!.Payment)
                .OrderByDescending(r => r.Status == RefundStatus.Pending)
                .ThenByDescending(r => r.RequestedDate)
                .ToListAsync();
            return View(refunds);
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRefund(int id, string? adminNote, bool manualRefund = false)
        {
            var refund = await _context.RefundRequests
                .Include(r => r.Order).ThenInclude(o => o!.Payment)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (refund == null) return NotFound();

            var payment = refund.Order?.Payment;

            if (payment == null)
            {
                TempData["Error"] = "No payment record found for this order.";
                return RedirectToAction(nameof(Refunds));
            }

            decimal amountToRefund = refund.RequestedRefundAmount > 0 ? refund.RequestedRefundAmount : payment.Amount;

            if (payment.Method == PaymentMethodType.COD || manualRefund)
            {
                payment.Status = PaymentStatus.Refunded;
                payment.RefundedAmount = amountToRefund;
                refund.Status = RefundStatus.Approved;
                refund.AdminNote = string.IsNullOrWhiteSpace(adminNote)
                    ? (payment.Method == PaymentMethodType.COD ? "Refunded manually (COD)." : "Refunded manually by Admin (SSLCommerz bypassed).")
                    : adminNote;
                refund.ProcessedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Refund for order #{refund.OrderId} approved — ৳{amountToRefund:0.00} marked refunded manually.";
                return RedirectToAction(nameof(Refunds));
            }

            if (string.IsNullOrEmpty(payment.BankTransactionId))
            {
                TempData["Error"] = "Can't process an automatic refund: no bank transaction ID was captured for this payment. " +
                                     "This can happen for older test orders paid before this feature existed. " +
                                     "You can check \"Process manually\" below to mark it refunded without calling SSLCommerz.";
                return RedirectToAction(nameof(Refunds));
            }

            var result = await _sslCommerz.InitiateRefundAsync(
                payment.BankTransactionId,
                amountToRefund,
                string.IsNullOrWhiteSpace(adminNote) ? $"Refund for order #{refund.OrderId}" : adminNote);

            if (result.Success)
            {
                payment.Status = PaymentStatus.Refunded;
                payment.RefundReferenceId = result.RefundReferenceId;
                payment.RefundedAmount = amountToRefund;
                refund.Status = RefundStatus.Approved;
                refund.AdminNote = adminNote;
                refund.ProcessedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Refund for order #{refund.OrderId} approved and processed via SSLCommerz (ref: {result.RefundReferenceId}).";
            }
            else
            {
                TempData["Error"] = $"SSLCommerz rejected the refund request: {result.Message}. " +
                                     "The refund request was NOT approved — try again, or check \"Process manually\" below to bypass SSLCommerz.";
            }

            return RedirectToAction(nameof(Refunds));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRefund(int id, string? adminNote)
        {
            var refund = await _context.RefundRequests.FindAsync(id);
            if (refund == null) return NotFound();

            refund.Status = RefundStatus.Rejected;
            refund.AdminNote = adminNote;
            refund.ProcessedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Refund request for order #{refund.OrderId} rejected.";
            return RedirectToAction(nameof(Refunds));
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRefundRequest(int id)
        {
            var refund = await _context.RefundRequests.FindAsync(id);
            if (refund == null) return NotFound();

            _context.RefundRequests.Remove(refund);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Refund request #{id} deleted.";
            return RedirectToAction(nameof(Refunds));
        }

    }
}