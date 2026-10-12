namespace WaterbusSystem.Application.Features.Incidents;

/// <summary>
/// Cấu hình Weather Guard (section "Weather" / biến môi trường Weather__*).
/// </summary>
public class WeatherOptions
{
    public const string SectionName = "Weather";

    public bool Enabled { get; set; } = true;

    /// <summary>"OpenWeatherMap" hoặc "Fake".</summary>
    public string Provider { get; set; } = "Fake";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5/";

    /// <summary>Lịch quét dạng cron (mặc định mỗi 15 phút).</summary>
    public string CronExpression { get; set; } = "*/15 * * * *";

    /// <summary>Chỉ xét Route có Trip khởi hành trong khoảng này (giờ) hoặc đang chạy.</summary>
    public int LookaheadHours { get; set; } = 3;

    /// <summary>Ngưỡng gió trung bình (m/s). 10.6 m/s ≈ 38 km/h.</summary>
    public double WindSpeedThresholdMs { get; set; } = 10.6;

    /// <summary>Ngưỡng gió giật (m/s) coi là nghiêm trọng. 17.2 m/s ≈ cấp 8 Beaufort.</summary>
    public double GustThresholdMs { get; set; } = 17.2;

    /// <summary>Incident thời tiết Open của cùng Route trong khoảng này (giờ) được cập nhật thay vì tạo mới.</summary>
    public int AlertWindowHours { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 2;

    public FakeWeatherOptions Fake { get; set; } = new();
}

/// <summary>
/// Thời tiết giả lập cho demo (vd đặt WindSpeedMs = 15 để kích hoạt cảnh báo).
/// </summary>
public class FakeWeatherOptions
{
    public double WindSpeedMs { get; set; } = 3;
    public double? WindGustMs { get; set; }
    public int ConditionCode { get; set; } = 800;
    public string Description { get; set; } = "trời quang";
}
