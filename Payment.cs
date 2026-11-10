using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore; // Required for [Index]

namespace AsianFoodRestaurant.Models
{
    public class Payment
    {

  
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public PaymentMethodType Method { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        // SSLCommerz identifiers
        public string? TransactionId { get; set; }   // tran_id we generate
        public string? ValidationId { get; set; }     // val_id returned by SSLCommerz
        public string? BankTransactionId { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime? PaymentDate { get; set; }

        // Set once a refund is successfully initiated via SSLCommerz
        public string? RefundReferenceId { get; set; }

        // Actual amount refunded - may be less than Amount for a partial
       
        public decimal? RefundedAmount { get; set; }
    }
}
