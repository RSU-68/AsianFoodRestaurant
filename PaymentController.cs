using AsianFoodRestaurant.Data;
using AsianFoodRestaurant.Models;
using AsianFoodRestaurant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; 

namespace AsianFoodRestaurant.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISslCommerzService _sslCommerz;
        private readonly UserManager<ApplicationUser> _userManager;

        public PaymentController(ApplicationDbContext context, ISslCommerzService sslCommerz, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _sslCommerz = sslCommerz;
            _userManager = userManager;
        }

        // Kicks off the SSLCommerz sandbox session and redirects the customer to their gateway page.
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Pay(int orderId)
        {
            var order = await _context.Orders.Include(o => o.Payment).Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.Payment == null) return NotFound();

            var tranId = $"TXN{order.Id}{DateTime.Now:yyyyMMddHHmmss}";
            order.Payment.TransactionId = tranId;
            await _context.SaveChangesAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var result = await _sslCommerz.InitiatePaymentAsync(
                transactionId: tranId,
                amount: order.TotalAmount,
                customerName: order.Customer!.FullName,
                customerEmail: order.Customer.Email ?? "",
                customerAddress: order.DeliveryAddress ?? "N/A",
                successUrl: $"{baseUrl}/Payment/Success",
                failUrl: $"{baseUrl}/Payment/Fail",
                cancelUrl: $"{baseUrl}/Payment/Cancel",
                ipnUrl: $"{baseUrl}/Payment/Ipn");

            if (result.Success && result.GatewayPageUrl != null)
                return Redirect(result.GatewayPageUrl);

            TempData["Error"] = $"Could not start payment: {result.Message}. Make sure your SSLCommerz sandbox Store ID/Password are set in appsettings.json.";
            return RedirectToAction("MyOrders", "Customer");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Success(string tran_id, string val_id)
        {
            var payment = await _context.Payments.Include(p => p.Order).FirstOrDefaultAsync(p => p.TransactionId == tran_id);
            if (payment == null) return NotFound();

            var validation = await _sslCommerz.ValidatePaymentAsync(val_id);
            if (validation.IsValid)
            {
                payment.Status = PaymentStatus.Paid;
                payment.ValidationId = val_id;
                payment.BankTransactionId = validation.BankTransactionId;
                payment.PaymentDate = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Payment successful! Here is your invoice.";
                return RedirectToAction("Invoice", new { orderId = payment.OrderId });
            }

            TempData["Error"] = "Payment could not be validated.";
            return RedirectToAction("MyOrders", "Customer");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Fail(string tran_id)
        {
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TransactionId == tran_id);
            if (payment != null)
            {
                payment.Status = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
            }
            TempData["Error"] = "Payment failed. You can try again from My Orders.";
            return RedirectToAction("MyOrders", "Customer");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Cancel(string tran_id)
        {
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TransactionId == tran_id);
            if (payment != null)
            {
                payment.Status = PaymentStatus.Cancelled;
                await _context.SaveChangesAsync();
            }
            TempData["Error"] = "Payment cancelled.";
            return RedirectToAction("MyOrders", "Customer");
        }

        // SSLCommerz server-to-server notification (optional but recommended for production)
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Ipn(string tran_id, string val_id)
        {
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TransactionId == tran_id);
            if (payment != null)
            {
                var validation = await _sslCommerz.ValidatePaymentAsync(val_id);
                if (validation.IsValid)
                {
                    payment.Status = PaymentStatus.Paid;
                    payment.ValidationId = val_id;
                    payment.BankTransactionId = validation.BankTransactionId;
                    payment.PaymentDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            return Ok();
        }

        [Authorize]
        public async Task<IActionResult> Invoice(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();
            return View(order);
        }
    }
}
