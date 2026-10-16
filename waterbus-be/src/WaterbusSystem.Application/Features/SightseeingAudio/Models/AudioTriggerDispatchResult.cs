namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Kết quả dispatch yêu cầu trigger tới Onboard Player hoặc message queue/hub.
/// </summary>
public record AudioTriggerDispatchResult
{
    public bool IsDispatched { get; init; }
    /// <summary>Player xác nhận đã nhận và chấp nhận xếp lệnh phát; không đồng nghĩa đã bắt đầu phát.</summary>
    public bool IsAcceptedByPlayer { get; init; }
    public Guid RequestId { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset DispatchedAt { get; init; } = DateTimeOffset.UtcNow;

    public static AudioTriggerDispatchResult PlayerAccepted(Guid requestId, string? message = null) =>
        new() { IsDispatched = true, IsAcceptedByPlayer = true, RequestId = requestId, Message = message };

    public static AudioTriggerDispatchResult SentWithoutPlayerAck(Guid requestId) =>
        new() { IsDispatched = true, IsAcceptedByPlayer = false, RequestId = requestId };

    public static AudioTriggerDispatchResult Failed(Guid requestId, string message) =>
        new() { IsDispatched = false, RequestId = requestId, Message = message };
}
