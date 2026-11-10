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
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICartService _cartService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISslCommerzService _sslCommerz;

        public CustomerController(ApplicationDbContext context, ICartService cartService, UserManager<ApplicationUser> userManager, ISslCommerzService sslCommerz)
        {
            _context = context;
            _cartService = cartService;
            _userManager = userManager;
            _sslCommerz = sslCommerz;
        }

        // ---------- Cart ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int menuItemId, int quantity = 1)
        {
            var item = await _context.MenuItems.FindAsync(menuItemId);
            if (item == null) return NotFound();

            var (success, message) = _cartService.AddToCart(item, quantity);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction("Menu", "Home");
        }

        public IActionResult Cart()
        {
            var cart = _cartService.GetCart();
            ViewBag.Total = _cartService.GetTotal();
            return View(cart);
        }


        [HttpPost]
        public async Task<IActionResult> ChangeQuantity(int menuItemId, int delta)
        {
            var item = await _context.MenuItems.FindAsync(menuItemId);
            int stock = item?.StockQuantity ?? 0;

            var (success, message) = _cartService.UpdateQuantity(menuItemId, delta, stock);

            return Json(new
            {
                success,
                message,
                cart = _cartService.GetCart(),
                total = _cartService.GetTotal()
            });
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int menuItemId)
        {
            _cartService.RemoveItem(menuItemId);
            return RedirectToAction(nameof(Cart));
        }

        // ---------- Checkout ----------
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = _cartService.GetCart();
            if (!cart.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Menu", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            var model = new CheckoutViewModel
            {
                Items = cart,
                Total = _cartService.GetTotal(),
                DeliveryAddress = user?.Address ?? ""
            };
            return View(model);
        }

        
        // Actual payment happens next via PaymentController/SSLCommerz (or is recorded as COD).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cart = _cartService.GetCart();
            if (!cart.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Menu", "Home");
            }

            // Re-validate stock at checkout time in case it changed
            foreach (var line in cart)
            {
                var menuItem = await _context.MenuItems.FindAsync(line.MenuItemId);
                if (menuItem == null || line.Quantity > menuItem.StockQuantity)
                {
                    TempData["Error"] = $"Stock out for {line.Name}. Please update your cart.";
                    return RedirectToAction(nameof(Cart));
                }
            }

            var user = await _userManager.GetUserAsync(User);

            var order = new Order
            {
                CustomerId = user!.Id,
                OrderDate = DateTime.Now,
                Status = OrderStatus.Pending,
                DeliveryAddress = model.DeliveryAddress,
                TotalAmount = cart.Sum(c => c.LineTotal),
                OrderItems = cart.Select(c => new OrderItem
                {
                    MenuItemId = c.MenuItemId,
                    MenuItemName = c.Name,
                    UnitPrice = c.Price,
                    Quantity = c.Quantity,
                    LineTotal = c.LineTotal
                }).ToList()
            };

            // Deduct stock immediately when the order is placed
            foreach (var line in cart)
            {
                var menuItem = await _context.MenuItems.FindAsync(line.MenuItemId);
                menuItem!.StockQuantity -= line.Quantity;
            }

            order.Payment = new Payment
            {
                Method = model.PaymentMethod,
                Amount = order.TotalAmount,
                Status = model.PaymentMethod == PaymentMethodType.COD ? PaymentStatus.Pending : PaymentStatus.Pending,
                InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            _cartService.ClearCart();

            // COD needs no gateway - go straight to confirmation.
            if (model.PaymentMethod == PaymentMethodType.COD)
            {
                TempData["Success"] = "Order placed! You will pay Cash on Delivery.";
                return RedirectToAction(nameof(MyOrders));
            }

            
            return RedirectToAction("Pay", "Payment", new { orderId = order.Id });
        }

        // ---------- Orders dashboard ----------
        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .Include(o => o.ClaimedByEmployee)
                .Where(o => o.CustomerId == user!.Id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

           

            var ratedOrderIds = await _context.Ratings
                .Where(r => r.CustomerId == user!.Id)
                .Select(r => r.OrderId)
                .ToListAsync();
            ViewBag.RatedOrderIds = ratedOrderIds;

          
            var refunds = await _context.RefundRequests
                .Where(r => r.CustomerId == user!.Id)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            ViewBag.RefundsByOrder = refunds
                .GroupBy(r => r.OrderId)
                .ToDictionary(g => g.Key, g => g.First());

            return View(orders);
        }

//-----Cancel order---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == user!.Id);

            if (order == null) return NotFound();

            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Accepted)
            {
                TempData["Error"] = "This order can no longer be cancelled — it's already being prepared or has been completed.";
                return RedirectToAction(nameof(MyOrders));
            }

            bool wasAlreadyAccepted = order.Status == OrderStatus.Accepted;
            decimal refundPercentage = wasAlreadyAccepted ? 0.20m : 1.00m;

            foreach (var line in order.OrderItems)
            {
                var menuItem = await _context.MenuItems.FindAsync(line.MenuItemId);
                if (menuItem != null)
                    menuItem.StockQuantity += line.Quantity;
            }

            order.Status = OrderStatus.Cancelled;

            decimal refundAmount = 0m;
            bool refundRequested = false;

            if (order.Payment != null && order.Payment.Status == PaymentStatus.Paid)
            {
                refundAmount = Math.Round(order.Payment.Amount * refundPercentage, 2);
                refundRequested = true;

                var reason = wasAlreadyAccepted
                    ? $"Order cancelled by customer after admin acceptance — 20% refund (৳{refundAmount:0.00}) applies per cancellation policy, 80% fee retained."
                    : $"Order cancelled by customer while still pending — full refund (৳{refundAmount:0.00}) applies.";

                _context.RefundRequests.Add(new RefundRequest
                {
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    Reason = reason,
                    Status = RefundStatus.Pending,
                    RequestedRefundAmount = refundAmount
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = refundRequested
                ? $"Order #{order.Id} cancelled. A refund request for ৳{refundAmount:0.00} ({refundPercentage:P0}) has been sent to the restaurant for approval."
                : $"Order #{order.Id} cancelled.";
            return RedirectToAction(nameof(MyOrders));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == user!.Id);

            if (order == null) return NotFound();

            if (order.Status != OrderStatus.Cancelled && order.Status != OrderStatus.Declined)
            {
                TempData["Error"] = "Only cancelled or declined orders can be deleted from your history.";
                return RedirectToAction(nameof(MyOrders));
            }

         
            var relatedRatings = await _context.Ratings.Where(r => r.OrderId == orderId).ToListAsync();
            _context.Ratings.RemoveRange(relatedRatings);

            var relatedRefunds = await _context.RefundRequests.Where(r => r.OrderId == orderId).ToListAsync();
            _context.RefundRequests.RemoveRange(relatedRefunds);

            try
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Order #{orderId} removed from your history.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = $"Order #{orderId} couldn't be deleted because other records still reference it.";
            }

            return RedirectToAction(nameof(MyOrders));
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == user!.Id);

            if (order == null) return NotFound();
            return View(order);
        }

        // ---------- Ratings ----------
        [HttpGet]
        public async Task<IActionResult> RateOrder(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == user!.Id);

            if (order == null) return NotFound();

            if (order.Status != OrderStatus.Delivered)
            {
                TempData["Error"] = "You can only rate an order after it has been delivered.";
                return RedirectToAction(nameof(MyOrders));
            }

            var alreadyRated = await _context.Ratings.AnyAsync(r => r.OrderId == orderId);
            if (alreadyRated)
            {
                TempData["Error"] = "You've already rated this order.";
                return RedirectToAction(nameof(MyOrders));
            }

            return View(new RateOrderViewModel { OrderId = orderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateOrder(RateOrderViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == model.OrderId && o.CustomerId == user!.Id);

            if (order == null) return NotFound();
            if (!ModelState.IsValid) return View(model);

            var alreadyRated = await _context.Ratings.AnyAsync(r => r.OrderId == model.OrderId);
            if (alreadyRated)
            {
                TempData["Error"] = "You've already rated this order.";
                return RedirectToAction(nameof(MyOrders));
            }

            _context.Ratings.Add(new Rating
            {
                OrderId = model.OrderId,
                CustomerId = user!.Id,
                Stars = model.Stars,
                Comment = model.Comment
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Thanks for your feedback!";
            return RedirectToAction(nameof(MyOrders));
        }

        // ---------- Complaints ----------
        public async Task<IActionResult> Complaints()
        {
            var user = await _userManager.GetUserAsync(User);
            var complaints = await _context.Complaints
                .Where(c => c.CustomerId == user!.Id)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();
            return View(complaints);
        }

        [HttpGet]
        public async Task<IActionResult> FileComplaint(int? orderId)
        {
            var user = await _userManager.GetUserAsync(User);

            ViewBag.Orders = await _context.Orders
                .Where(o => o.CustomerId == user!.Id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(new FileComplaintViewModel { OrderId = orderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FileComplaint(FileComplaintViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (!ModelState.IsValid)
            {
                ViewBag.Orders = await _context.Orders
                    .Where(o => o.CustomerId == user!.Id)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();
                return View(model);
            }

            _context.Complaints.Add(new Complaint
            {
                CustomerId = user!.Id,
                OrderId = model.OrderId,
                Subject = model.Subject,
                Message = model.Message
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your complaint has been submitted. Our team will respond soon.";
            return RedirectToAction(nameof(Complaints));
        }

        // ---------- Refunds ----------
        [HttpGet]
        public async Task<IActionResult> RequestRefund(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == user!.Id);

            if (order == null) return NotFound();

            if (order.Payment == null || order.Payment.Status != PaymentStatus.Paid)
            {
                TempData["Error"] = "Only paid orders can be refunded.";
                return RedirectToAction(nameof(MyOrders));
            }

            var existing = await _context.RefundRequests
                .AnyAsync(r => r.OrderId == orderId && r.Status == RefundStatus.Pending);
            if (existing)
            {
                TempData["Error"] = "You already have a pending refund request for this order.";
                return RedirectToAction(nameof(MyOrders));
            }

            return View(new RequestRefundViewModel { OrderId = orderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRefund(RequestRefundViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == model.OrderId && o.CustomerId == user!.Id);

            if (order == null) return NotFound();
            if (!ModelState.IsValid) return View(model);

            if (order.Payment == null || order.Payment.Status != PaymentStatus.Paid)
            {
                TempData["Error"] = "Only paid orders can be refunded.";
                return RedirectToAction(nameof(MyOrders));
            }

            _context.RefundRequests.Add(new RefundRequest
            {
                OrderId = model.OrderId,
                CustomerId = user!.Id,
                Reason = model.Reason,
                RequestedRefundAmount = order.Payment.Amount
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your refund request has been submitted for review.";
            return RedirectToAction(nameof(MyOrders));
        }
    }
}
