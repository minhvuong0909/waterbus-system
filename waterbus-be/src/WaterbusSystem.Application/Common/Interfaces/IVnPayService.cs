namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Cổng thanh toán VNPAY.
///
/// LƯU Ý QUAN TRỌNG: thuật toán checksum của pay/ipn (ASCII sort + UrlEncode + HMAC-SHA512)
/// KHÁC hoàn toàn với checksum của querydr/refund (nối các field bằng dấu "|" theo thứ tự
/// cố định, KHÔNG urlencode, KHÔNG sort). Vì vậy cần hai hàm checksum riêng biệt.
/// </summary>
public interface IVnPayService
{
    /// <summary>
    /// Sinh URL thanh toán cho lệnh pay (vnp_Command=pay), có ký HMAC-SHA512 kiểu payment.
    /// </summary>
    string CreatePaymentUrl(string merchantReference, decimal amount, string orderInfo, string ipAddress);

    /// <summary>
    /// Kiểm tra tính hợp lệ của chữ ký HMAC-SHA512 nhận từ Webhook IPN / Callback.
    /// So sánh bằng thời gian cố định chống Timing Side-Channel Attack.
    /// </summary>
    bool ValidateSignature(IReadOnlyDictionary<string, string> query);

    /// <summary>Kiểm tra vnp_TmnCode khớp merchant đã cấu hình, chặn IPN giả mạo.</summary>
    bool IsValidTmnCode(string tmnCode);

    /// <summary>
    /// BR-PAY-01: truy vấn chủ động trạng thái giao dịch (vnp_Command=querydr) để đối soát khi
    /// IPN đến trễ, đến lỗi, hoặc cần xác minh khoản hoàn đã thực sự chuyển đến người nhận.
    /// </summary>
    Task<VnPayQueryResult> QueryTransactionAsync(
        string merchantReference, string transactionDate, string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi yêu cầu hoàn tiền tới VNPAY (vnp_Command=refund).
    /// BR-RF-10: nếu gateway không hỗ trợ hoặc lỗi không xử lý được, Admin thực hiện thủ công.
    /// </summary>
    Task<VnPayRefundResult> RefundAsync(
        VnPayRefundRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Kết quả truy vấn giao dịch (vnp_Command=querydr).</summary>
public sealed record VnPayQueryResult
{
    public bool IsSuccess { get; init; }
    public string ResponseCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? TransactionStatus { get; init; }
    public string? TransactionNo { get; init; }
    public string? TransactionType { get; init; }
    public string? BankCode { get; init; }
    public string? PayDate { get; init; }
    public decimal? Amount { get; init; }
}

/// <summary>Yêu cầu hoàn tiền theo đặc tả API merchant_webapi của VNPAY.</summary>
public sealed record VnPayRefundRequest
{
    /// <summary>vnp_RequestId - mã merchant tự sinh, duy nhất, không trùng trong ngày.</summary>
    public required string RequestId { get; init; }

    /// <summary>vnp_TxnRef - mã giao dịch thanh toán gốc đã gửi VNPAY.</summary>
    public required string MerchantReference { get; init; }

    /// <summary>vnp_TransactionNo - mã giao dịch ghi nhận tại VNPAY (tuỳ chọn).</summary>
    public string? GatewayReference { get; init; }

    /// <summary>Số tiền hoàn, nhỏ hơn hoặc bằng số tiền giao dịch gốc.</summary>
    public required decimal Amount { get; init; }

    /// <summary>vnp_CreateBy - người khởi tạo hoàn tiền.</summary>
    public required string CreatedBy { get; init; }

    /// <summary>vnp_TransactionDate - thời gian giao dịch gốc tại merchant, GMT+7, yyyyMMddHHmmss.</summary>
    public required string TransactionDate { get; init; }

    public required string IpAddress { get; init; }

    public required string OrderInfo { get; init; }

    /// <summary>true = hoàn toàn phần, false = hoàn một phần.</summary>
    public bool IsFullRefund { get; init; }
}

/// <summary>Kết quả hoàn tiền từ VNPAY.</summary>
public sealed record VnPayRefundResult
{
    public bool IsSuccess { get; init; }
    public string ResponseCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? TransactionStatus { get; init; }
    public string? RefundTransactionNo { get; init; }

    /// <summary>
    /// true khi gateway cần thêm thời gian xử lý (vnp_TransactionStatus = 05 hoặc 06) và
    /// yêu cầu phải được đối soát tiếp bằng QueryDr trước khi ghi Succeeded.
    /// </summary>
    public bool IsPendingProcessing { get; init; }
}