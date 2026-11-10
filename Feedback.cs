using System.ComponentModel.DataAnnotations;

namespace AsianFoodRestaurant.Models
{
    public enum ComplaintStatus
    {
        Open,
        InProgress,
        Resolved
    }

    public enum RefundStatus
    {
        Pending,
        Approved,
        Rejected
    }


    public class Rating
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public string CustomerId { get; set; } = string.Empty;
        public ApplicationUser? Customer { get; set; }

        [Range(1, 5)]
        public int Stars { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public class Complaint
    {
        public int Id { get; set; }

        public string CustomerId { get; set; } = string.Empty;
        public ApplicationUser? Customer { get; set; }

       
        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        [Required, StringLength(150)]
        public string Subject { get; set; } = string.Empty;

        [Required, StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;

        [StringLength(1000)]
        public string? AdminResponse { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? RespondedDate { get; set; }
    }


    public class RefundRequest
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public string CustomerId { get; set; } = string.Empty;
        public ApplicationUser? Customer { get; set; }

        [Required, StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        public RefundStatus Status { get; set; } = RefundStatus.Pending;

        [StringLength(500)]
        public string? AdminNote { get; set; }

        public DateTime RequestedDate { get; set; } = DateTime.Now;
        public DateTime? ProcessedDate { get; set; }

       
        // Admin's approval uses this figure rather than recalculating it.
        public decimal RequestedRefundAmount { get; set; }
    }
}