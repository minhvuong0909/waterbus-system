namespace WaterbusSystem.Application.DTOs.Responses.OnboardAudio;

/// <summary>
/// Trạng thái cho Onboard Player đồng bộ sau (re)connect. PlaybackAllowed=false khi chuyến không phải Sightseeing EnRoute
/// hoặc có thông báo an toàn mở (BR-EXP-06). Reason là mã ổn định để client hiển thị/log.
/// </summary>
public record OnboardAudioStateResponse(Guid BoatId, Guid? TripId, string? TripStatus, string? TripType,
    bool PlaybackAllowed, string Reason);

public record PlaybackStateAck(Guid RequestId, bool Applied);
