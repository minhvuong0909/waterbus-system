namespace WaterbusSystem.Application.Features.Refunds;

/// <summary>Chế độ hoàn tiền của môi trường. Mặc định <see cref="RefundMode.Demo"/> (an toàn).</summary>
public enum RefundMode
{
    /// <summary>
    /// Sandbox/MVP: KHÔNG bao giờ gọi API Refund của VNPay. Refund mới đi qua Admin xác nhận thủ công và được
    /// đánh dấu demo (không có tiền thật được hệ thống chuyển).
    /// </summary>
    Demo = 0,

    /// <summary>Môi trường đã được VNPay cấp quyền Refund: dùng gateway tự động (hành vi trước đây).</summary>
    Gateway = 1
}

public sealed class RefundOptions
{
    public const string SectionName = "Refunds";

    public RefundMode Mode { get; set; } = RefundMode.Demo;
}
