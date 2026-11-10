using AsianFoodRestaurant.Models;

namespace AsianFoodRestaurant.Services
{
    public interface ICartService
    {
        List<CartItem> GetCart();

        // Returns (success, message). message is set e.g. "Stock out" when it fails.
        (bool Success, string Message) AddToCart(MenuItem item, int quantity);

        // delta = +1 or -1 from the +/- buttons. If resulting quantity <= 0, item is removed.
        (bool Success, string Message) UpdateQuantity(int menuItemId, int delta, int currentStock);

        void RemoveItem(int menuItemId);
        void ClearCart();
        decimal GetTotal();
    }
}
