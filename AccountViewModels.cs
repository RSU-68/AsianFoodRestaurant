using System.ComponentModel.DataAnnotations;
using AsianFoodRestaurant.Models;

namespace AsianFoodRestaurant.ViewModels
{
    public class RegisterViewModel
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Address { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public class CreateEmployeeViewModel
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public EmployeeRole EmployeeRole { get; set; }
    }

    public class MenuItemFormViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string? Category { get; set; }

        [Range(0, 100000)]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        public string? ExistingImagePath { get; set; }

        public IFormFile? ImageFile { get; set; }
    }

    public class CheckoutViewModel
    {
        public List<CartItem> Items { get; set; } = new();
        public decimal Total { get; set; }

        [Required]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required]
        public PaymentMethodType PaymentMethod { get; set; }
    }

    public class RateOrderViewModel
    {
        public int OrderId { get; set; }

        [Range(1, 5, ErrorMessage = "Please choose a rating from 1 to 5 stars.")]
        public int Stars { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }
    }

    public class FileComplaintViewModel
    {
        public int? OrderId { get; set; }

        [Required, StringLength(150)]
        public string Subject { get; set; } = string.Empty;

        [Required, StringLength(1000)]
        public string Message { get; set; } = string.Empty;
    }

    public class RespondComplaintViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(1000)]
        public string AdminResponse { get; set; } = string.Empty;

        [Required]
        public ComplaintStatus Status { get; set; }
    }

    public class RequestRefundViewModel
    {
        public int OrderId { get; set; }

        [Required, StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class DecideRefundViewModel
    {
        public int Id { get; set; }

        [StringLength(500)]
        public string? AdminNote { get; set; }
    }
}