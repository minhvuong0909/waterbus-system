using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.Payment;

/// <summary>
/// Cổng thanh toán VNPAY với ký HMAC-SHA512 chống Timing Attack.
///
/// HAI KIỂU CHECKSUM KHÁC NHAU - đây là điểm dễ sai nhất khi tích hợp VNPAY:
///   1. pay / ipn / return : sắp xếp key theo ASCII, UrlEncode từng cặp key=value, nối bằng "&".
///   2. querydr / refund  : nối giá trị các field theo THỨ TỰ CỐ ĐỊNH bằng dấu "|", KHÔNG urlencode.
/// Dùng chung một hàm checksum sẽ khiến mọi yêu cầu refund/querydr bị từ chối với mã 97.
/// </summary>
public class VnPayService : IVnPayService
{
    /// <summary>Endpoint pay dùng cho vnp_Command=pay.</summary>
    private const string DefaultBaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary>Endpoint merchant_webapi dùng chung cho vnp_Command=querydr và vnp_Command=refund.</summary>
    private const string DefaultWebApiUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<VnPayService> _logger;

    public VnPayService(IConfiguration configuration, HttpClient httpClient, ILogger<VnPayService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string CreatePaymentUrl(string merchantReference, decimal amount, string orderInfo, string ipAddress, DateTimeOffset? createdAt = null, DateTimeOffset? expiresAt = null)
    {
        var hashSecret = RequireSecret();
        var baseUrl = _configuration["VnPay:BaseUrl"] ?? DefaultBaseUrl;
        var returnUrl = _configuration["VnPay:ReturnUrl"] ?? "http://localhost:5173/payment/callback";

        // Thứ tự ASCII bắt buộc của VNPAY cho vnp_Command=pay.
        var requestData = new SortedList<string, string>(new VnPayAsciiComparer())
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = RequireTmnCode(),
            // Số tiền nhân 100 theo quy ư���c của VNPAY (15.000 VND -> 1.500.000)
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
            ["vnp_CreateDate"] = GatewayTime(createdAt ?? DateTimeOffset.UtcNow),
            ["vnp_ExpireDate"] = GatewayTime(expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(15)),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = ipAddress,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = merchantReference
        };

        var queryString = BuildSortedQueryString(requestData);
        var secureHash = HmacSha512(hashSecret, queryString);

        return $"{baseUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    public bool ValidateSignature(IReadOnlyDictionary<string, string> query)
    {
        var hashSecret = RequireSecret();
        var responseData = new SortedList<string, string>(new VnPayAsciiComparer());

        string receivedSecureHash = string.Empty;
        foreach (var (key, value) in query)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith("vnp_", StringComparison.Ordinal))
            {
                continue;
            }

            if (key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase))
            {
                receivedSecureHash = value;
            }
            else if (!key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                responseData[key] = value;
            }
        }

        if (string.IsNullOrEmpty(receivedSecureHash))
        {
            return false;
        }

        var calculatedHash = HmacSha512(hashSecret, BuildSortedQueryString(responseData));

        // =========================================================================
        // BẢO MẬT: SO SÁNH CONSTANT-TIME bằng CryptographicOperations.FixedTimeEquals
        // Đảm bảo thời gian so sánh không lộ thông tin qua kênh thời gian phản hồi
        // =========================================================================
        var calculatedBytes = Encoding.UTF8.GetBytes(calculatedHash);
        var receivedBytes = Encoding.UTF8.GetBytes(receivedSecureHash.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(calculatedBytes, receivedBytes);
    }

    public bool IsValidTmnCode(string tmnCode) =>
        string.Equals(tmnCode, RequireTmnCode(), StringComparison.Ordinal);

    public async Task<VnPayQueryResult> QueryTransactionAsync(
        string merchantReference, string transactionDate, string ipAddress, CancellationToken cancellationToken = default)
    {
        var createDate = GatewayTime(DateTimeOffset.UtcNow);
        var requestId = GenerateRequestId("QDR");

        // Thứ tự field trong checksum là CỐ ĐỊNH theo đặc tả merchant_webapi, không sort.
        var data = string.Join("|",
            requestId,
            "2.1.0",
            "querydr",
            RequireTmnCode(),
            merchantReference,
            transactionDate,
            createDate,
            ipAddress,
            "Query transaction");

        var body = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = requestId,
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "querydr",
            ["vnp_TmnCode"] = RequireTmnCode(),
            ["vnp_TxnRef"] = merchantReference,
            ["vnp_TransactionDate"] = transactionDate,
            ["vnp_CreateDate"] = createDate,
            ["vnp_IpAddr"] = ipAddress,
            ["vnp_OrderInfo"] = "Query transaction",
            ["vnp_SecureHash"] = HmacSha512(RequireSecret(), data)
        };

        var response = await PostAsync(body, cancellationToken);
        _logger.LogInformation("[VNPAY QUERYDR] TxnRef={MerchantReference} -> {Code} {Message}",
            merchantReference, response.GetValueOrDefault("vnp_ResponseCode"), response.GetValueOrDefault("vnp_Message"));

        var transactionStatus = response.GetValueOrDefault("vnp_TransactionStatus");

        return new VnPayQueryResult
        {
            // 00 = yêu cầu querydr thực hiện thành công (kể cả giao dịch đã hoàn).
            IsSuccess = response.GetValueOrDefault("vnp_ResponseCode") == "00",
            MerchantReference = response.GetValueOrDefault("vnp_TxnRef"),
            ResponseCode = response.GetValueOrDefault("vnp_ResponseCode") ?? string.Empty,
            Message = response.GetValueOrDefault("vnp_Message") ?? string.Empty,
            TransactionStatus = transactionStatus,
            TransactionNo = response.GetValueOrDefault("vnp_TransactionNo"),
            TransactionType = response.GetValueOrDefault("vnp_TransactionType"),
            BankCode = response.GetValueOrDefault("vnp_BankCode"),
            PayDate = response.GetValueOrDefault("vnp_PayDate"),
            Amount = decimal.TryParse(response.GetValueOrDefault("vnp_Amount"), out var amount) ? amount : null
        };
    }

    public async Task<VnPayRefundResult> RefundAsync(
        VnPayRefundRequest request, CancellationToken cancellationToken = default)
    {
        var createDate = GatewayTime(DateTimeOffset.UtcNow);
        var requestId = request.RequestId;
        if (requestId.Length is < 1 or > 32 || requestId.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Refund RequestId must be 1-32 ASCII alphanumeric characters.");

        // vnp_TransactionType: 02 = hoàn trả toàn phần, 03 = hoàn trả một phần.
        var transactionType = request.IsFullRefund ? "02" : "03";

        // Thứ tự field trong checksum là CỐ ĐỊNH theo đặc tả merchant_webapi, không sort.
        var data = string.Join("|",
            requestId,
            "2.1.0",
            "refund",
            RequireTmnCode(),
            transactionType,
            request.MerchantReference,
            ((long)(request.Amount * 100)).ToString(),
            request.GatewayReference ?? string.Empty,
            request.TransactionDate,
            request.CreatedBy,
            createDate,
            request.IpAddress,
            request.OrderInfo);

        var body = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = requestId,
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "refund",
            ["vnp_TmnCode"] = RequireTmnCode(),
            ["vnp_TransactionType"] = transactionType,
            ["vnp_TxnRef"] = request.MerchantReference,
            ["vnp_Amount"] = ((long)(request.Amount * 100)).ToString(),
            ["vnp_TransactionNo"] = request.GatewayReference ?? string.Empty,
            ["vnp_TransactionDate"] = request.TransactionDate,
            ["vnp_CreateBy"] = request.CreatedBy,
            ["vnp_CreateDate"] = createDate,
            ["vnp_IpAddr"] = request.IpAddress,
            ["vnp_OrderInfo"] = request.OrderInfo,
            ["vnp_SecureHash"] = HmacSha512(RequireSecret(), data)
        };

        var response = await PostAsync(body, cancellationToken);

        var responseCode = response.GetValueOrDefault("vnp_ResponseCode") ?? string.Empty;
        var transactionStatus = response.GetValueOrDefault("vnp_TransactionStatus");

        _logger.LogInformation("[VNPAY REFUND] TxnRef={MerchantReference} Amount={Amount} -> {Code} {Message}",
            request.MerchantReference, request.Amount, responseCode, response.GetValueOrDefault("vnp_Message"));

        return new VnPayRefundResult
        {
            // 00 = yêu cầu refund đã được tiếp nhận thành công.
            IsSuccess = responseCode == "00",
            MerchantReference = response.GetValueOrDefault("vnp_TxnRef"),
            ResponseCode = responseCode,
            Message = response.GetValueOrDefault("vnp_Message") ?? string.Empty,
            TransactionStatus = transactionStatus,
            RefundTransactionNo = response.GetValueOrDefault("vnp_TransactionNo"),
            // 05 = VNPAY đang xử lý giao dịch hoàn tiền, 06 = đã gửi yêu cầu hoàn sang ngân hàng.
            IsPendingProcessing = transactionStatus is "05" or "06"
        };
    }

    private async Task<Dictionary<string, string>> PostAsync(
        Dictionary<string, string> body, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HttpTimeout);

        var endpoint = _configuration["VnPay:WebApiUrl"] ?? DefaultWebApiUrl;

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(endpoint, body, timeout.Token);
            var json = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[VNPAY] HTTP {StatusCode} từ {Endpoint}", response.StatusCode, endpoint);
                return new Dictionary<string, string>
                {
                    ["vnp_ResponseCode"] = "99",
                    ["vnp_Message"] = $"HTTP {(int)response.StatusCode}"
                };
            }

            var parsed = ParseResponse(json);
            if (!ValidateMerchantResponse(parsed, body))
                return new Dictionary<string, string> { ["vnp_ResponseCode"] = "97", ["vnp_Message"] = "Unverified gateway response; reconciliation required" };
            return parsed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Timeout hoặc lỗi mạng KHÔNG được coi là hoàn tiền thất bại - có thể gateway đã
            // nhận yêu cầu. Phải để ở trạng thái chờ đối soát (BR-RF-10).
            _logger.LogError(ex, "[VNPAY] Không gọi được API merchant_webapi tại {Endpoint}", endpoint);

            return new Dictionary<string, string>
            {
                ["vnp_ResponseCode"] = "99",
                ["vnp_Message"] = "Không kết nối được tới VNPAY. Cần đối soát lại giao dịch."
            };
        }
    }

    /// <summary>
    /// Đặc tả VNPAY không cam kết mọi trường trả về đều là chuỗi. Nếu deserialize thẳng sang
    /// Dictionary&lt;string,string&gt; thì một giá trị dạng số sẽ làm cả lời gọi API ném exception,
    /// biến lỗi kỹ thuật thành lỗi nghiệp vụ. Vì vậy đọc theo JsonElement rồi chuyển sang chuỗi.
    /// </summary>
    private static Dictionary<string, string> ParseResponse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                    _ => property.Value.GetRawText()
                };
            }

            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return new Dictionary<string, string>
            {
                ["vnp_ResponseCode"] = "99",
                ["vnp_Message"] = "Không đọc được phản hồi JSON từ VNPAY."
            };
        }
    }

    /// <summary>Kiểu checksum cho pay/ipn: ASCII sort + UrlEncode + nối bằng "&".</summary>
    private static string BuildSortedQueryString(SortedList<string, string> data)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in data)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            builder.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        return builder.ToString().TrimEnd('&');
    }

    private static string HmacSha512(string key, string input)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(input);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLower();
    }

    private bool ValidateMerchantResponse(Dictionary<string, string> response, Dictionary<string, string> request)
    {
        var names = new List<string> { "vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message",
            "vnp_TmnCode", "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo",
            "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo" };
        if (request["vnp_Command"] == "querydr") names.AddRange(["vnp_PromotionCode", "vnp_PromotionAmount"]);
        var expected = HmacSha512(RequireSecret(), string.Join("|", names.Select(n => response.GetValueOrDefault(n, ""))));
        var received = response.GetValueOrDefault("vnp_SecureHash", "").ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(received))
            && response.GetValueOrDefault("vnp_TmnCode") == RequireTmnCode()
            && response.GetValueOrDefault("vnp_Command") == request["vnp_Command"]
            && response.GetValueOrDefault("vnp_TxnRef") == request["vnp_TxnRef"]
            && (request["vnp_Command"] != "refund" ||
                (response.GetValueOrDefault("vnp_Amount") == request["vnp_Amount"] &&
                 response.GetValueOrDefault("vnp_TransactionType") == request["vnp_TransactionType"]));
    }

    public static string GatewayTime(DateTimeOffset instant) => instant.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss");

    private string RequireTmnCode() => RequireSetting("VnPay:TmnCode");

    // Khoá rỗng vẫn ký HMAC được: nếu không chặn, ai cũng tự ký được IPN "đã thanh toán" khi quên cấu hình.
    private string RequireSecret() => RequireSetting("VnPay:HashSecret");

    private string RequireSetting(string key)
    {
        var value = _configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Thiếu cấu hình {key}.")
            : value;
    }

    /// <summary>
    /// vnp_RequestId tối đa 32 ký tự alphanumeric, không trùng lặp trong ngày.
    /// Dùng timestamp + hậu tố ngẫu nhiên từ CSPRNG để tránh trùng khi gateway retry.
    /// </summary>
    private static string GenerateRequestId(string prefix) =>
        $"{prefix}{DateTime.Now:yyyyMMddHHmmss}{RandomNumberGenerator.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 6)}";
}
