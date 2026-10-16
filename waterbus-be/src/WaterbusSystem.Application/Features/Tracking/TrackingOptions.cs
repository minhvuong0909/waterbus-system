namespace WaterbusSystem.Application.Features.Tracking;

/// <summary>
/// Cấu hình theo dõi vị trí tàu (section "Tracking" trong appsettings / biến môi trường Tracking__*).
/// </summary>
public class TrackingOptions
{
    public const string SectionName = "Tracking";

    /// <summary>Khoảng cách tối thiểu (giây) giữa 2 lần ghi BoatPositionEvent của cùng 1 tàu.</summary>
    public int PersistIntervalSeconds { get; set; } = 15;

    /// <summary>Không nhận vị trí có ObservedAt cũ hơn khoảng này (phút).</summary>
    public int MaxPositionAgeMinutes { get; set; } = 60;

    public BoatSimulatorOptions Simulator { get; set; } = new();
}

/// <summary>
/// Giả lập GPS theo Route (BR-TRK-01 cho phép khi demo). Mặc định TẮT.
/// </summary>
public class BoatSimulatorOptions
{
    public bool Enabled { get; set; }

    /// <summary>Chu kỳ phát vị trí giả lập (giây).</summary>
    public int IntervalSeconds { get; set; } = 3;

    /// <summary>
    /// Chỉ dùng khi demo: giả lập cả Trip đang Scheduled/Boarding (chưa có API chuyển trạng thái Trip).
    /// Mặc định chỉ giả lập Trip EnRoute/Suspended.
    /// </summary>
    public bool IncludeScheduledTrips { get; set; }
}
