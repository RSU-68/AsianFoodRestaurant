using System.Text.Json;
using AsianFoodRestaurant.Models;
using Microsoft.AspNetCore.Http;

namespace AsianFoodRestaurant.Services
{
    public class CartService : ICartService
    {
        private const string CartSessionKey = "Cart";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ISession Session => _httpContextAccessor.HttpContext!.Session;

        public List<CartItem> GetCart()
        {
            var json = Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(json))
                return new List<CartItem>();

            return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>();
        }

        private void SaveCart(List<CartItem> cart)
        {
            Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
        }

        public (bool Success, string Message) AddToCart(MenuItem item, int quantity)
        {
            if (quantity <= 0) quantity = 1;

            var cart = GetCart();
            var existing = cart.FirstOrDefault(c => c.MenuItemId == item.Id);
            int requestedTotal = (existing?.Quantity ?? 0) + quantity;

            if (requestedTotal > item.StockQuantity)
            {
                return (false, item.StockQuantity == 0
                    ? "Stock out. You can not order this food."
                    : $"Only {item.StockQuantity} left in stock. You can not order that many.");
            }

            if (existing != null)
            {
                existing.Quantity = requestedTotal;
                existing.AvailableStock = item.StockQuantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    MenuItemId = item.Id,
                    Name = item.Name,
                    Price = item.Price,
                    Quantity = quantity,
                    ImagePath = item.ImagePath,
                    AvailableStock = item.StockQuantity
                });
            }

            SaveCart(cart);
            return (true, "Added to cart.");
        }

        public (bool Success, string Message) UpdateQuantity(int menuItemId, int delta, int currentStock)
        {
            var cart = GetCart();
            var existing = cart.FirstOrDefault(c => c.MenuItemId == menuItemId);
            if (existing == null) return (false, "Item not found in cart.");

            int newQty = existing.Quantity + delta;

            if (newQty <= 0)
            {
                cart.Remove(existing);
                SaveCart(cart);
                return (true, "Item removed from cart.");
            }

            if (newQty > currentStock)
            {
                return (false, currentStock == 0
                    ? "Stock out. You can not order this food."
                    : $"Only {currentStock} left in stock. You can not order that many.");
            }

            existing.Quantity = newQty;
            existing.AvailableStock = currentStock;
            SaveCart(cart);
            return (true, "Cart updated.");
        }

        public void RemoveItem(int menuItemId)
        {
            var cart = GetCart();
            cart.RemoveAll(c => c.MenuItemId == menuItemId);
            SaveCart(cart);
        }

        public void ClearCart()
        {
            Session.Remove(CartSessionKey);
        }

        public decimal GetTotal()
        {
            return GetCart().Sum(c => c.LineTotal);
        }
    }
}
