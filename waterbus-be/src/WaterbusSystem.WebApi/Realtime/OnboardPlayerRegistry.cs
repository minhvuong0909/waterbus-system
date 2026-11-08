using System.Collections.Concurrent;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.WebApi.Realtime;

/// <summary>
/// Theo dõi Onboard Player đang kết nối theo Boat và các lượt chờ ACK. Singleton, in-process:
/// SignalR hiện chạy một instance (chưa có backplane) nên đủ cho phạm vi hiện tại.
/// </summary>
public sealed class OnboardPlayerRegistry : IOnboardPlayerRegistry
{
    private readonly ConcurrentDictionary<string, (Guid BoatId, Guid CaptainId, long Seq)> _connections = new();
    private long _seq;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> _acks = new();

    public void Register(Guid boatId, string connectionId, Guid captainId) => _connections[connectionId] = (boatId, captainId, Interlocked.Increment(ref _seq));
    public void Unregister(string connectionId) => _connections.TryRemove(connectionId, out _);
    public bool IsConnected(Guid boatId) => _connections.Values.Any(c => c.BoatId == boatId);

    /// <summary>Theo thứ tự kết nối, cũ → mới. Kết nối MỚI NHẤT là player đang hoạt động (active) của Boat.</summary>
    public IReadOnlyList<(string ConnectionId, Guid CaptainId)> Connections(Guid boatId) =>
        _connections.Where(c => c.Value.BoatId == boatId).OrderBy(c => c.Value.Seq).Select(c => (c.Key, c.Value.CaptainId)).ToList();

    public bool IsActive(string connectionId) =>
        _connections.TryGetValue(connectionId, out var me) &&
        _connections.Values.Where(c => c.BoatId == me.BoatId).Max(c => c.Seq) == me.Seq;

    public IOnboardAckWait PrepareAck(Guid requestId)
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _acks[requestId] = source;
        return new Wait(this, requestId, source);
    }

    public bool SignalAck(Guid requestId) => _acks.TryGetValue(requestId, out var source) && source.TrySetResult(true);

    private sealed class Wait(OnboardPlayerRegistry owner, Guid requestId, TaskCompletionSource<bool> source) : IOnboardAckWait
    {
        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            try { return await source.Task.WaitAsync(timeout, cancellationToken); }
            catch (TimeoutException) { return false; }
        }

        public void Dispose() => owner._acks.TryRemove(new KeyValuePair<Guid, TaskCompletionSource<bool>>(requestId, source));
    }
}
