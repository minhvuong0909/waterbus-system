using System.Collections.Concurrent;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Identity;

public sealed class CaptainConnections : ICaptainConnections
{
    private readonly ConcurrentDictionary<string, (Guid CaptainId, Action Abort)> _connections = new();
    public void Register(Guid captainId, string connectionId, Action abort) => _connections[connectionId] = (captainId, abort);
    public void Unregister(string connectionId) => _connections.TryRemove(connectionId, out _);
    public void Revoke(Guid captainId)
    {
        foreach (var (id, connection) in _connections)
            if (connection.CaptainId == captainId && _connections.TryRemove(id, out _)) connection.Abort();
    }
}
