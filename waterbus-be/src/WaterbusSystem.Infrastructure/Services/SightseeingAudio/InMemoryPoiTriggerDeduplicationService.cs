using System.Collections.Concurrent;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.SightseeingAudio;

/// <summary>
/// Cài đặt deduplication bằng ConcurrentDictionary in-memory.
/// Phù hợp cho single-instance (development, demo). Production multi-instance cần đổi sang Redis.
/// Key = "trip:{tripId}:poi:{poiId}:visit:{visitId}" → đã trigger hay chưa.
/// </summary>
public sealed class InMemoryPoiTriggerDeduplicationService : IPoiTriggerDeduplicationService
{
    // Trạng thái: null = chưa gì, "reserved" = đang giữ chỗ, "triggered" = đã dispatch xong
    private readonly ConcurrentDictionary<string, string> _state = new();

    private const string Reserved = "reserved";
    private const string Triggered = "triggered";

    public Task<bool> HasBeenTriggeredAsync(
        Guid tripId, Guid poiId, string? visitId = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tripId, poiId, visitId);
        var result = _state.TryGetValue(key, out var status) && status == Triggered;
        return Task.FromResult(result);
    }

    public Task<bool> TryReserveAsync(
        Guid tripId, Guid poiId, string? visitId = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tripId, poiId, visitId);
        var added = _state.TryAdd(key, Reserved);
        return Task.FromResult(added);
    }

    public Task MarkAsTriggeredAsync(
        Guid tripId, Guid poiId, string? visitId = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tripId, poiId, visitId);
        _state[key] = Triggered;
        return Task.CompletedTask;
    }

    public Task ReleaseReservationAsync(
        Guid tripId, Guid poiId, string? visitId = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tripId, poiId, visitId);
        // Xóa có điều kiện nguyên tử: một ACK đồng thời đã chuyển sang triggered không bị xóa.
        ((ICollection<KeyValuePair<string, string>>)_state)
            .Remove(new KeyValuePair<string, string>(key, Reserved));
        return Task.CompletedTask;
    }

    private static string BuildKey(Guid tripId, Guid poiId, string? visitId)
        => $"trip:{tripId}:poi:{poiId}:visit:{visitId ?? "default"}";
}
