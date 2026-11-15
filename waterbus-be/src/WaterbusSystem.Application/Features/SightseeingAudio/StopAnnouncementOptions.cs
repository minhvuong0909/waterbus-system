namespace WaterbusSystem.Application.Features.SightseeingAudio;

/// <summary>
/// Cấu hình thời gian chạy chuyến và ngưỡng báo hiệu bến, dùng cho cả Commuter lẫn Sightseeing.
/// Đọc lại được khi đang chạy (IOptionsMonitor) nên Admin đổi cấu hình không cần restart.
/// Các trường "Seconds" là thời lượng dành sẵn để dựng ngân sách thời gian của một chặng.
/// </summary>
public sealed class StopAnnouncementOptions
{
    public const string SectionName = "StopAnnouncement";

    /// <summary>
    /// Tổng thời gian một chuyến đi từ bến đầu đến bến cuối (giây), bao gồm cả thời gian đậu tại các bến.
    /// Hệ thống chia đều cho các chặng. Chỉ dùng khi <see cref="UseSimulatedSegmentDuration"/> = true.
    /// </summary>
    public double TripDurationSeconds { get; set; } = 180;

    /// <summary>
    /// Thời gian đậu tại mỗi bến (giây), tính trong <see cref="TripDurationSeconds"/>.
    /// Phải đủ dài để phát trọn thông báo cập bến.
    /// </summary>
    public double DwellTimeSeconds { get; set; } = 6;

    /// <summary>
    /// Ngưỡng tiến độ trong chặng (0.0–1.0) để phát thông báo "gần tới bến".
    /// </summary>
    public double NearThreshold { get; set; } = 0.75;

    /// <summary>
    /// Dùng <see cref="TripDurationSeconds"/> thay vì thời gian chuyến thực tế trong CSDL.
    /// </summary>
    public bool UseSimulatedSegmentDuration { get; set; } = true;

    /// <summary>
    /// Số chặng dùng để dựng ngân sách khi CSDL chưa có tuyến nào. Khi đã có tuyến,
    /// hệ thống lấy số chặng lớn nhất trong các tuyến đang hoạt động.
    /// </summary>
    public int EstimatedSegmentCount { get; set; } = 4;

    /// <summary>Thời lượng dành sẵn cho câu thông báo rời bến.</summary>
    public double DepartingAnnouncementSeconds { get; set; } = 5;

    /// <summary>Thời lượng dành sẵn cho câu thông báo gần tới bến.</summary>
    public double NearAnnouncementSeconds { get; set; } = 4;

    /// <summary>Thời lượng dành sẵn cho câu thông báo cập bến.</summary>
    public double ArrivedAnnouncementSeconds { get; set; } = 4;

    /// <summary>Khoảng đệm bắt buộc giữa hai câu liên tiếp để không bị cắt tiếng.</summary>
    public double SafetyGapSeconds { get; set; } = 1;

    /// <summary>
    /// Thời lượng tối đa của một bài thuyết minh địa danh theo chính sách (giây).
    /// Một bài thuyết minh dài hơn ngân sách thời gian của chặng sẽ không được publish.
    /// </summary>
    public double MaxNarrationSeconds { get; set; } = 10;

    /// <summary>Số ký tự trung bình một giây khi đọc tiếng Việt, dùng quy đổi ra giới hạn ký tự.</summary>
    public double NarrationCharactersPerSecond { get; set; } = 14;

    public StopAnnouncementOptions Clone() => (StopAnnouncementOptions)MemberwiseClone();
}
