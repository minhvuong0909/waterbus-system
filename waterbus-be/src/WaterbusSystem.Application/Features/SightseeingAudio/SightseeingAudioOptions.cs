namespace WaterbusSystem.Application.Features.SightseeingAudio;

/// <summary>Cấu hình audio Sightseeing. GPS geofence chỉ bật có chủ đích (EnableGpsTriggerEvaluation).</summary>
public sealed class SightseeingAudioOptions
{
    public const string SectionName = "SightseeingAudio";
    public bool EnableGpsTriggerEvaluation { get; set; }
    /// <summary>Thời gian chờ Onboard Player xác nhận đã nhận lệnh (giây); quá hạn = chưa ACK, retry ở lần GPS sau.</summary>
    public int AckTimeoutSeconds { get; set; } = 3;
    /// <summary>Số lần thử tối đa cho một lượt ghé POI; hết lượt thì không tự phát lại.</summary>
    public int MaxDispatchAttempts { get; set; } = 3;
    /// <summary>Lease giữ chỗ: reservation treo (instance chết giữa chừng) quá thời gian này được giành lại.</summary>
    public int ReservationLeaseSeconds { get; set; } = 30;
    /// <summary>Mẫu GPS cũ hơn ngưỡng này (giây) không được kích hoạt audio; 0 = không kiểm tra.</summary>
    public int MaxPositionAgeSeconds { get; set; } = 30;
}
