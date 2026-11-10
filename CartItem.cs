namespace AsianFoodRestaurant.Models
{
   
    public class CartItem
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? ImagePath { get; set; }
        public int AvailableStock { get; set; }

        public decimal LineTotal => Price * Quantity;
    }
}
