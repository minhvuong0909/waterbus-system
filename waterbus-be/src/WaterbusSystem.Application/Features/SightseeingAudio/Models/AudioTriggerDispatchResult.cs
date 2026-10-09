namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Kết quả dispatch yêu cầu trigger tới Onboard Player hoặc message queue/hub.
/// </summary>
public record AudioTriggerDispatchResult
{
    public bool IsDispatched { get; init; }
    public Guid RequestId { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset DispatchedAt { get; init; } = DateTimeOffset.UtcNow;

    public static AudioTriggerDispatchResult Success(Guid requestId, string? message = null) =>
        new() { IsDispatched = true, RequestId = requestId, Message = message };

    public static AudioTriggerDispatchResult Failed(Guid requestId, string message) =>
        new() { IsDispatched = false, RequestId = requestId, Message = message };
}
