using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;

namespace WaterbusSystem.UnitTests.SightseeingAudio.Fakes;

/// <summary>
/// Fake in-memory implementation của <see cref="IPoiTriggerDeduplicationService"/> phục vụ unit tests.
/// Quản lý chống kích hoạt lặp theo bộ khóa (TripId + PoiId + VisitId) trong cùng lượt ghé (BR-EXP-05).
/// </summary>
public sealed class InMemoryPoiTriggerDeduplicationService : IPoiTriggerDeduplicationService
{
    private readonly HashSet<string> _triggeredKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reservedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public IReadOnlyCollection<string> TriggeredKeys
    {
        get
        {
            lock (_lock)
            {
                return _triggeredKeys.ToList();
            }
        }
    }

    public int TotalTriggeredCount
    {
        get
        {
            lock (_lock)
            {
                return _triggeredKeys.Count;
            }
        }
    }

    public Task<bool> HasBeenTriggeredAsync(
        Guid tripId,
        Guid poiId,
        string? visitId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildKey(tripId, poiId, visitId);

        lock (_lock)
        {
            return Task.FromResult(_triggeredKeys.Contains(key));
        }
    }

    public Task MarkAsTriggeredAsync(
        Guid tripId,
        Guid poiId,
        string? visitId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildKey(tripId, poiId, visitId);

        lock (_lock)
        {
            _reservedKeys.Remove(key);
            _triggeredKeys.Add(key);
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryReserveAsync(
        Guid tripId,
        Guid poiId,
        string? visitId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildKey(tripId, poiId, visitId);
        lock (_lock)
        {
            if (_triggeredKeys.Contains(key) || !_reservedKeys.Add(key))
                return Task.FromResult(false);
            return Task.FromResult(true);
        }
    }

    public Task ReleaseReservationAsync(
        Guid tripId,
        Guid poiId,
        string? visitId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _reservedKeys.Remove(BuildKey(tripId, poiId, visitId));
        }
        return Task.CompletedTask;
    }

    // Test-only helper; policy reset lượt ghé production vẫn chờ team chốt.
    public Task ResetForTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prefix = $"{tripId}:";

        lock (_lock)
        {
            _triggeredKeys.RemoveWhere(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            _reservedKeys.RemoveWhere(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _triggeredKeys.Clear();
            _reservedKeys.Clear();
        }
    }

    private static string BuildKey(Guid tripId, Guid poiId, string? visitId) =>
        $"{tripId}:{poiId}:{visitId ?? "default"}";
}
