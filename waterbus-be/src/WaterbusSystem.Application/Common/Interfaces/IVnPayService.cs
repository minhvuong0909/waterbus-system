namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng dịch vụ tích hợp cổng thanh toán VNPAY
/// </summary>
public interface IVnPayService
{
    /// <summary>
    /// Sinh URL chuyển hướng khách hàng sang cổng thanh toán VNPAY kèm chữ ký HMAC-SHA512
    /// </summary>
    string CreatePaymentUrl(string bookingCode, decimal amount, string orderInfo, string ipAddress);

    /// <summary>
    /// Kiểm tra chữ ký HMAC-SHA512 an toàn thời gian cố định (FixedTimeEquals) từ Webhook IPN / Callback
    /// </summary>
    bool ValidateSignature(IReadOnlyDictionary<string, string> query);

    /// <summary>
    /// Kiểm tra vnp_TmnCode nhận được từ Webhook IPN có khớp với mã merchant đã cấu hình hay không
    /// </summary>
    bool IsValidTmnCode(string tmnCode);
}
