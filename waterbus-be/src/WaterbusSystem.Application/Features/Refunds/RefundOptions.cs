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

    /// <summary>
    /// true khi URL trỏ tới VNPay KHÔNG phải sandbox (vd pay.vnpay.vn, vnpayment.vn production). Host tự dựng/mock/localhost
    /// hoặc để trống thì KHÔNG bị coi là tiền thật. Dùng để chặn Demo khi cấu hình endpoint tiền thật.
    /// </summary>
    public static bool IsLiveVnPayUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        // FQDN có dấu chấm cuối (pay.vnpay.vn.) là cùng một host DNS: chuẩn hoá trước khi so sánh.
        var host = uri.Host.ToLowerInvariant().TrimEnd('.');
        var isVnPay = host == "vnpay.vn" || host.EndsWith(".vnpay.vn") || host == "vnpayment.vn" || host.EndsWith(".vnpayment.vn");
        return isVnPay && !host.StartsWith("sandbox.");
    }
}
