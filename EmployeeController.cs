using AsianFoodRestaurant.Data;
using AsianFoodRestaurant.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AsianFoodRestaurant.Controllers
{
    [Authorize(Roles = "Employee")]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Orders admin has accepted, not yet claimed by anyone
        public async Task<IActionResult> Dashboard()
        {
            var available = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .Where(o => o.Status == OrderStatus.Accepted && o.ClaimedByEmployeeId == null)
                .OrderBy(o => o.OrderDate)
                .ToListAsync();
            return View(available);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClaimOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.ClaimedByEmployeeId != null)
            {
                TempData["Error"] = "This order has already been claimed by another employee.";
                return RedirectToAction(nameof(Dashboard));
            }

            var user = await _userManager.GetUserAsync(User);
            order.ClaimedByEmployeeId = user!.Id;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order #{order.Id} claimed. Find it in My Orders.";
            return RedirectToAction(nameof(MyOrders));
        }

        // Orders claimed by the current employee
        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .Where(o => o.ClaimedByEmployeeId == user!.Id &&
                            (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Preparing))
                .OrderBy(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        // Shows as "Preparing" on the customer dashboard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPreparing(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = OrderStatus.Preparing;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Order #{order.Id} marked as Preparing.";
            return RedirectToAction(nameof(MyOrders));
        }

        // Shows as "Delivered" on the customer dashboard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCompleted(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = OrderStatus.Delivered;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Order #{order.Id} marked as Delivered.";
            return RedirectToAction(nameof(MyOrders));
        }
    }
}
