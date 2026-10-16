using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Locking;

/// <summary>RedLock.net giữ mutex critical-section; DB transaction quyết định tính đúng của inventory.</summary>
public sealed class RedisDistributedLockService : IDistributedLockService, IDisposable
{
    private readonly List<ConnectionMultiplexer> _connections = [];
    private readonly RedLockFactory _factory;
    private readonly ILogger<RedisDistributedLockService> _logger;
    public RedisDistributedLockService(IConfiguration configuration, ILogger<RedisDistributedLockService> logger)
    {
        _logger = logger;
        var endpoints = configuration.GetSection("RedisLock:Connections").Get<string[]>()
            ?? [configuration.GetConnectionString("RedisConnection") ?? "localhost:6379"];
        if (endpoints.Length == 0) throw new InvalidOperationException("RedisLock requires at least one connection.");
        foreach (var endpoint in endpoints)
        {
            var options = ConfigurationOptions.Parse(endpoint);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1000; options.AsyncTimeout = 1000;
            _connections.Add(ConnectionMultiplexer.Connect(options));
        }
        _factory = RedLockFactory.Create(_connections.Select(c => (RedLockMultiplexer)c).ToList());
    }
    public async Task<IAsyncDisposable?> AcquireLockAsync(string resourceKey, TimeSpan expiryTime,
        TimeSpan waitTime, TimeSpan retryTime, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_connections.All(c => !c.IsConnected))
        {
            _logger.LogWarning("Redis offline; inventory vẫn được bảo vệ bởi SQL commercial transaction.");
            return new DatabaseProtectedLease();
        }
        var lease = await _factory.CreateLockAsync(resourceKey, expiryTime, waitTime, retryTime, cancellationToken);
        if (lease.IsAcquired) return new Lease(lease);
        lease.Dispose();
        return null;
    }
    public void Dispose()
    {
        _factory.Dispose();
        foreach (var connection in _connections) connection.Dispose();
    }
    private sealed class Lease(IRedLock lease) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { lease.Dispose(); return ValueTask.CompletedTask; }
    }
    private sealed class DatabaseProtectedLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
