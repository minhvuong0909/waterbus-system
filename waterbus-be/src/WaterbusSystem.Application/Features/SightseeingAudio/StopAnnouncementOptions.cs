namespace WaterbusSystem.Application.Features.SightseeingAudio;

/// <summary>
/// Cấu hình thuật toán báo hiệu bến (gần bến / đã tới bến) cho cả hai loại chuyến.
/// Có thể thay đổi khi demo mà không cần restart (IOptionsMonitor).
/// </summary>
public sealed class StopAnnouncementOptions
{
    public const string SectionName = "StopAnnouncement";

    /// <summary>
    /// Thời gian giả lập toàn bộ chuyến đi từ bến đầu đến bến cuối (giây). Mặc định 180s (3 phút) cho demo, đổi được từ UI.
    /// Hệ thống sẽ tự động chia đều thời gian cho các chặng giữa các bến trong chuyến.
    /// Chỉ có tác dụng khi <see cref="UseSimulatedSegmentDuration"/> = true.
    /// </summary>
    public double SegmentDurationSeconds { get; set; } = 180;

    /// <summary>
    /// Dừng lại tại bến (giây) trước khi bắt đầu đoạn tiếp theo. Mặc định 3s.
    /// </summary>
    public double DwellTimeSeconds { get; set; } = 3;

    /// <summary>
    /// Ngưỡng (0.0–1.0) để phát thông báo "gần tới bến". Mặc định 0.8 (80%).
    /// </summary>
    public double NearThreshold { get; set; } = 0.8;

    /// <summary>
    /// Dùng SegmentDurationSeconds thay vì thời gian chuyến thực từ DB (chế độ demo).
    /// Nếu false: tính progress từ tổng thời gian thực của chuyến (production).
    /// </summary>
    public bool UseSimulatedSegmentDuration { get; set; } = true;
}
