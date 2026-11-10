using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AsianFoodRestaurant.Services
{
    public class SslCommerzService : ISslCommerzService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public SslCommerzService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<SslCommerzInitResult> InitiatePaymentAsync(
            string transactionId,
            decimal amount,
            string customerName,
            string customerEmail,
            string customerAddress,
            string successUrl,
            string failUrl,
            string cancelUrl,
            string ipnUrl)
        {
            var storeId = _configuration["SslCommerz:StoreId"];
            var storePassword = _configuration["SslCommerz:StorePassword"];
            var apiUrl = _configuration["SslCommerz:SandboxApiUrl"];

            var postData = new Dictionary<string, string>
            {
                { "store_id", storeId ?? "" },
                { "store_passwd", storePassword ?? "" },
                { "total_amount", amount.ToString("0.00") },
                { "currency", "BDT" },
                { "tran_id", transactionId },
                { "success_url", successUrl },
                { "fail_url", failUrl },
                { "cancel_url", cancelUrl },
                { "ipn_url", ipnUrl },
                { "cus_name", customerName },
                { "cus_email", customerEmail },
                { "cus_add1", customerAddress },
                { "cus_city", "Dhaka" },
                { "cus_country", "Bangladesh" },
                { "cus_phone", "01700000000" },
                { "shipping_method", "NO" },
                { "product_name", "Restaurant Order" },
                { "product_category", "Food" },
                { "product_profile", "general" }
            };

            try
            {
                using var content = new FormUrlEncodedContent(postData);
                var response = await _httpClient.PostAsync(apiUrl, content);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;

                if (status == "SUCCESS" && root.TryGetProperty("GatewayPageURL", out var url))
                {
                    return new SslCommerzInitResult { Success = true, GatewayPageUrl = url.GetString() };
                }

                var failedReason = root.TryGetProperty("failedreason", out var fr) ? fr.GetString() : "Unknown error initiating payment.";
                return new SslCommerzInitResult { Success = false, Message = failedReason };
            }
            catch (Exception ex)
            {
                return new SslCommerzInitResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<SslCommerzValidationResult> ValidatePaymentAsync(string valId)
        {
            var storeId = _configuration["SslCommerz:StoreId"];
            var storePassword = _configuration["SslCommerz:StorePassword"];
            var validationUrl = _configuration["SslCommerz:SandboxValidationUrl"];

            var url = $"{validationUrl}?val_id={valId}&store_id={storeId}&store_passwd={storePassword}&format=json";

            try
            {
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
                var isValid = status == "VALID" || status == "VALIDATED";

                return new SslCommerzValidationResult
                {
                    IsValid = isValid,
                    BankTransactionId = root.TryGetProperty("bank_tran_id", out var btid) ? btid.GetString() : null,
                    Amount = root.TryGetProperty("amount", out var amt) ? amt.GetString() : null
                };
            }
            catch
            {
                return new SslCommerzValidationResult { IsValid = false };
            }
        }
        public async Task<SslCommerzRefundResult> InitiateRefundAsync(
    string bankTransactionId,
    decimal refundAmount,
    string refundRemarks)
        {
            var storeId = _configuration["SslCommerz:StoreId"];
            var storePassword = _configuration["SslCommerz:StorePassword"];

            var url = "https://sandbox.sslcommerz.com/validator/api/merchantTransIDvalidationAPI.php" +
                       $"?bank_tran_id={Uri.EscapeDataString(bankTransactionId)}" +
                       $"&refund_amount={refundAmount:0.00}" +
                       $"&refund_remarks={Uri.EscapeDataString(refundRemarks)}" +
                       $"&store_id={storeId}&store_passwd={storePassword}&format=json";

            try
            {
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(json);
                }
                catch (JsonException)
                {
                    var snippet = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                    return new SslCommerzRefundResult
                    {
                        Success = false,
                        Message = $"SSLCommerz returned a non-JSON response: \"{snippet}\""
                    };
                }

                using var _ = doc;
                var root = doc.RootElement;

                var apiConnect = root.TryGetProperty("APIConnect", out var ac) ? ac.GetString() : null;
                var refundRefId = root.TryGetProperty("refund_ref_id", out var rid) ? rid.GetString() : null;
                var errorReason = root.TryGetProperty("errorReason", out var er) ? er.GetString() : null;

                if (apiConnect == "DONE")
                {
                    return new SslCommerzRefundResult { Success = true, RefundReferenceId = refundRefId };
                }

                return new SslCommerzRefundResult
                {
                    Success = false,
                    Message = errorReason ?? "SSLCommerz did not accept the refund request."
                };
            }
            catch (Exception ex)
            {
                return new SslCommerzRefundResult { Success = false, Message = ex.Message };
            }
        }

    }
}
