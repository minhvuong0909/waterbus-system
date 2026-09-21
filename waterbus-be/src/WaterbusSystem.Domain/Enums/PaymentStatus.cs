namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Trạng thái giao dịch thanh toán qua cổng VNPAY / MoMo
/// </summary>
public enum PaymentStatus
{
    Pending = 1,   // Đang chờ khách quét mã hoặc nhập thẻ
    Success = 2,   // Giao dịch thành công (IPN VNPAY trả ResponseCode == 00)
    Failed = 3,    // Khách hủy hoặc giao dịch thất bại
    Refunded = 4   // Đã hoàn tiền
}
