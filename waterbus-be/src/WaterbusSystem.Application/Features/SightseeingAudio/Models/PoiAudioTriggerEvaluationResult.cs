namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Kết quả đánh giá quyết định trigger audio cho một vị trí tàu.
/// </summary>
public record PoiAudioTriggerEvaluationResult
{
    /// <summary>
    /// Cho biết hệ thống có quyết định kích hoạt phát audio hay không.
    /// </summary>
    public bool ShouldTrigger { get; init; }

    /// <summary>
    /// Lý do cho quyết định (kích hoạt thành công hoặc lý do từ chối cụ thể).
    /// </summary>
    public TriggerDecisionReason Reason { get; init; }

    /// <summary>
    /// Thông điệp mô tả chi tiết kết quả đánh giá.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    public Guid? TriggeredPoiId { get; init; }
    public string? TriggeredPoiName { get; init; }
    public Guid? TriggeredAudioGuideId { get; init; }
    public string? AudioAssetUri { get; init; }
    public double? DistanceMeters { get; init; }
    public int? TriggerRadiusMeters { get; init; }

    /// <summary>
    /// Yêu cầu trigger đã được gửi (nếu ShouldTrigger = true).
    /// </summary>
    public AudioTriggerRequest? TriggerRequest { get; init; }

    /// <summary>
    /// Kết quả dispatch tới Onboard Player (nếu ShouldTrigger = true).
    /// </summary>
    public AudioTriggerDispatchResult? DispatchResult { get; init; }

    /// <summary>
    /// Ranh giới xác nhận loa tàu đã phát âm thanh thực tế (BR-EXP-03).
    /// Backend CHỈ đề xuất / gửi lệnh; chưa nhận được tín hiệu ACK từ Onboard Player
    /// trên tàu thì giá trị này LUÔN là false. Tuyệt đối không tự xác nhận "đã phát ra loa".
    /// </summary>
    public bool HasOnboardPlaybackAcknowledged => false;

    public static PoiAudioTriggerEvaluationResult Skipped(TriggerDecisionReason reason, string message) =>
        new()
        {
            ShouldTrigger = false,
            Reason = reason,
            Message = message
        };

    public static PoiAudioTriggerEvaluationResult Triggered(
        Guid poiId,
        string poiName,
        Guid audioGuideId,
        string audioAssetUri,
        double distanceMeters,
        int triggerRadiusMeters,
        AudioTriggerRequest request,
        AudioTriggerDispatchResult dispatchResult) =>
        new()
        {
            ShouldTrigger = true,
            Reason = TriggerDecisionReason.Triggered,
            Message = $"Đã phát lệnh trigger audio cho POI '{poiName}' (khoảng cách {distanceMeters:F1}m / bán kính {triggerRadiusMeters}m).",
            TriggeredPoiId = poiId,
            TriggeredPoiName = poiName,
            TriggeredAudioGuideId = audioGuideId,
            AudioAssetUri = audioAssetUri,
            DistanceMeters = distanceMeters,
            TriggerRadiusMeters = triggerRadiusMeters,
            TriggerRequest = request,
            DispatchResult = dispatchResult
        };
}
