namespace WaterbusSystem.Application.Features.Refunds;

/// <summary>Chế độ hoàn tiền của môi trường. Mặc định <see cref="RefundMode.Manual"/> (an toàn: không tự chuyển tiền).</summary>
public enum RefundMode
{
    /// <summary>Môi trường đã được VNPay cấp quyền Refund: dùng gateway tự động.</summary>
    Gateway = 1,

    /// <summary>
    /// Hoàn tiền thủ công có kiểm soát (BR-RF-10): hệ thống KHÔNG BAO GIỜ gọi API Refund của VNPay. Admin tạo yêu cầu
    /// hoàn, xuất danh sách CSV cho nhân viên hoàn qua cổng merchant, rồi xác nhận kèm bằng chứng.
    /// Đây là quy trình thật, không phải mô phỏng.
    /// </summary>
    Manual = 2
}

public sealed class RefundOptions
{
    public const string SectionName = "Refunds";

    public RefundMode Mode { get; set; } = RefundMode.Manual;
}
