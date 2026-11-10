using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AsianFoodRestaurant.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string? Category { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(1, 100000)]
        public decimal Price { get; set; }

        // Relative path under wwwroot/uploads/menu, e.g. "/uploads/menu/xyz.jpg"
        public string? ImagePath { get; set; }

        
        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        // Computed convenience flag, not stored
        [NotMapped]
        public bool IsInStock => StockQuantity > 0;

       
    }
}
