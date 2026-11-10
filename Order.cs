using System.ComponentModel.DataAnnotations.Schema;

namespace AsianFoodRestaurant.Models
{
    public class Order
    {
        public int Id { get; set; }

        public string CustomerId { get; set; } = string.Empty;
        public ApplicationUser? Customer { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        public string? DeliveryAddress { get; set; }

       
        public string? ClaimedByEmployeeId { get; set; }
        public ApplicationUser? ClaimedByEmployee { get; set; }

        public List<OrderItem> OrderItems { get; set; } = new();

        public Payment? Payment { get; set; }
    }

    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

      
        public string MenuItemName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal LineTotal { get; set; }
    }
}
