namespace WaterbusSystem.WebApi.Realtime;

/// <summary>
/// Event <c>audioTrigger</c> (version 2) tới Onboard Player của đúng Boat. RequestId ổn định cho mỗi thông báo (một lượt ghé bến/địa danh):
/// retry dùng lại cùng Id, player phải khử trùng. Clips theo thứ tự phát ("vi" rồi "en"); PlaybackPath là đường dẫn tương đối
/// cần JWT Captain (không có URL công khai). Đây là LỆNH phát; chưa phải xác nhận đã phát ra loa (BR-EXP-03).
/// </summary>
public sealed record AudioTriggerMessage(
    Guid RequestId, Guid TripId, Guid BoatId, string Kind, Guid SubjectId, string SubjectName,
    IReadOnlyList<AudioTriggerClipMessage> Clips, double DistanceMeters, DateTimeOffset TriggeredAt)
{
    public int Version => 2;
}

public sealed record AudioTriggerClipMessage(string Language, string PlaybackPath, string ContentType, int DurationSeconds, string Text);

/// <summary>Event <c>audioControl</c>: Action = pause | recheck. Pause khi Trip không còn EnRoute hoặc có thông báo an toàn (BR-EXP-06); recheck = điều kiện cần đã đủ, client phải gọi GetState và chỉ tiếp tục khi playbackAllowed.</summary>
public sealed record AudioControlMessage(string Action, Guid? TripId, string Reason, DateTimeOffset At)
{
    public int Version => 1;
}
