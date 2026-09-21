using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.Payment;

/// <summary>
/// Dịch vụ tích hợp VNPAY với cơ chế sinh URL và xác thực chữ ký số HMAC-SHA512
/// Áp dụng thuật toán so sánh hằng số thời gian CryptographicOperations.FixedTimeEquals
/// để triệt tiêu hoàn toàn nguy cơ tấn công kênh kề Timing Side-Channel Attack
/// </summary>
public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;
    private readonly SortedList<string, string> _requestData = new(new VnPayAsciiComparer());
    private readonly SortedList<string, string> _responseData = new(new VnPayAsciiComparer());

    public VnPayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreatePaymentUrl(string bookingCode, decimal amount, string orderInfo, string ipAddress)
    {
        var vnpayConfig = _configuration.GetSection("VnPay");
        var tmnCode = vnpayConfig["TmnCode"] ?? "2QXUI4J4";
        var hashSecret = vnpayConfig["HashSecret"] ?? "RAOCTJRQAXNSYJXXTGZAHQUHIEUXVPUK";
        var baseUrl = vnpayConfig["BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var returnUrl = vnpayConfig["ReturnUrl"] ?? "http://localhost:5173/payment/callback";

        _requestData.Clear();
        _requestData.Add("vnp_Version", "2.1.0");
        _requestData.Add("vnp_Command", "pay");
        _requestData.Add("vnp_TmnCode", tmnCode);
        // Số tiền nhân 100 theo quy định của VNPAY (15.000 VNĐ -> 1.500.000)
        _requestData.Add("vnp_Amount", ((long)(amount * 100)).ToString());
        _requestData.Add("vnp_CreateDate", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        _requestData.Add("vnp_CurrCode", "VND");
        _requestData.Add("vnp_IpAddr", ipAddress);
        _requestData.Add("vnp_Locale", "vn");
        _requestData.Add("vnp_OrderInfo", orderInfo);
        _requestData.Add("vnp_OrderType", "other");
        _requestData.Add("vnp_ReturnUrl", returnUrl);
        _requestData.Add("vnp_TxnRef", bookingCode);

        var data = new StringBuilder();
        foreach (var (key, value) in _requestData)
        {
            if (!string.IsNullOrEmpty(value))
            {
                data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
            }
        }

        var queryString = data.ToString().TrimEnd('&');
        var secureHash = HmacSha512(hashSecret, queryString);
        return $"{baseUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của chữ ký số HMAC-SHA512 nhận được từ IPN Webhook
    /// </summary>
    public bool ValidateSignature(IReadOnlyDictionary<string, string> query)
    {
        var hashSecret = _configuration["VnPay:HashSecret"] ?? "RAOCTJRQAXNSYJXXTGZAHQUHIEUXVPUK";
        _responseData.Clear();

        string receivedSecureHash = string.Empty;
        foreach (var (key, value) in query)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
            {
                if (key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase))
                {
                    receivedSecureHash = value;
                }
                else
                {
                    _responseData.Add(key, value);
                }
            }
        }

        if (string.IsNullOrEmpty(receivedSecureHash))
        {
            return false;
        }

        var rawData = new StringBuilder();
        foreach (var (key, value) in _responseData)
        {
            if (!string.IsNullOrEmpty(value))
            {
                rawData.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
            }
        }

        var calculatedHash = HmacSha512(hashSecret, rawData.ToString().TrimEnd('&'));

        // =========================================================================
        // BẢO MẬT: SO SÁNH CONSTANT-TIME BẰNG CryptographicOperations.FixedTimeEquals
        // Đảm bảo thời gian so sánh độc lập với vị trí byte sai khác đầu tiên
        // =========================================================================
        var calculatedBytes = Encoding.UTF8.GetBytes(calculatedHash.ToLowerInvariant());
        var receivedBytes = Encoding.UTF8.GetBytes(receivedSecureHash.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(calculatedBytes, receivedBytes);
    }

    private static string HmacSha512(string key, string input)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(input);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLower();
    }
}
