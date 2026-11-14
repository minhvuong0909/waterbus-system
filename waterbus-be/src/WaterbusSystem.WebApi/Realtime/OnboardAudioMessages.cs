namespace WaterbusSystem.WebApi.Realtime;

/// <summary>
/// Event <c>audioTrigger</c> tới Onboard Player của đúng Boat. RequestId ổn định cho mỗi lượt ghé POI: retry dùng lại cùng Id,
/// player phải khử trùng. PlaybackPath là đường dẫn tương đối cần gửi kèm JWT Captain (không có URL công khai/SAS).
/// Đây là LỆNH phát; chưa phải xác nhận đã phát ra loa (BR-EXP-03).
/// </summary>
public sealed record AudioTriggerMessage(
    Guid RequestId, Guid TripId, Guid BoatId, Guid RoutePoiId, Guid PoiId, string PoiName, Guid AudioGuideId,
    string PlaybackPath, string ContentType, int? DurationSeconds, double DistanceMeters, DateTimeOffset TriggeredAt)
{
    public int Version => 1;
}

/// <summary>Event <c>audioControl</c>: Action = pause | recheck. Pause khi Trip không còn EnRoute hoặc có thông báo an toàn (BR-EXP-06); recheck = điều kiện cần đã đủ, client phải gọi GetState và chỉ tiếp tục khi playbackAllowed.</summary>
public sealed record AudioControlMessage(string Action, Guid? TripId, string Reason, DateTimeOffset At)
{
    public int Version => 1;
}
