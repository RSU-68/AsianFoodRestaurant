using AsianFoodRestaurant.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AsianFoodRestaurant.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Landing page: hero + a few featured (in-stock) dishes
        public async Task<IActionResult> Index()
        {
            var featured = await _context.MenuItems
                .Where(m => m.StockQuantity > 0)
                .OrderByDescending(m => m.Id)
                .Take(3)
                .ToListAsync();

            return View(featured);
        }

        // Full menu grid with search
        public async Task<IActionResult> Menu(string? search)
        {
            var query = _context.MenuItems.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(m =>
                    m.Name.Contains(term) ||
                    (m.Category != null && m.Category.Contains(term)) ||
                    (m.Description != null && m.Description.Contains(term)));
            }

            var items = await query.OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync();

            ViewBag.SearchTerm = search;
            return View(items);
        }

        public IActionResult Error() => View();

        public async Task<IActionResult> Ratings()
        {
            var ratings = await _context.Ratings
                .Include(r => r.Customer)
                .Include(r => r.Order).ThenInclude(o => o!.OrderItems)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            ViewBag.AverageRating = ratings.Any() ? ratings.Average(r => r.Stars) : 0;
            return View(ratings);
        }
    }
}