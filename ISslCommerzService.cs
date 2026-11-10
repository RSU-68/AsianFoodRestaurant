namespace AsianFoodRestaurant.Services
{
    public class SslCommerzInitResult
    {
        public bool Success { get; set; }
        public string? GatewayPageUrl { get; set; }
        public string? Message { get; set; }
    }

    public class SslCommerzValidationResult
    {
        public bool IsValid { get; set; }
        public string? BankTransactionId { get; set; }
        public string? Amount { get; set; }
    }

    public class SslCommerzRefundResult
    {
        public bool Success { get; set; }
        public string? RefundReferenceId { get; set; }
        public string? Message { get; set; }
    }

    public interface ISslCommerzService
    {
        Task<SslCommerzInitResult> InitiatePaymentAsync(
            string transactionId,
            decimal amount,
            string customerName,
            string customerEmail,
            string customerAddress,
            string successUrl,
            string failUrl,
            string cancelUrl,
            string ipnUrl);

        Task<SslCommerzValidationResult> ValidatePaymentAsync(string valId);

        Task<SslCommerzRefundResult> InitiateRefundAsync(
            string bankTransactionId,
            decimal refundAmount,
            string refundRemarks);
    }
}