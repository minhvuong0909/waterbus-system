using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.Accounts;

/// <summary>
/// Chặn gửi lặp bằng <c>SET NX EX</c> (nguyên tử). Redis không sẵn sàng ⇒ cho gửi (fail-open) vì đã có rate limit theo IP
/// và đây chỉ là lớp chống spam hộp thư, không phải kiểm soát truy cập.
/// </summary>
public sealed class RedisEmailCooldownStore : IEmailCooldownStore, IDisposable
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisEmailCooldownStore> _logger;

    public RedisEmailCooldownStore(IConfiguration configuration, ILogger<RedisEmailCooldownStore> logger)
    {
        _logger = logger;
        try
        {
            var options = ConfigurationOptions.Parse(configuration.GetConnectionString("RedisConnection") ?? "localhost:6379");
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1000;
            _redis = ConnectionMultiplexer.Connect(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không kết nối được Redis cho chặn gửi lặp email.");
        }
    }

    public async Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        if (_redis is not { IsConnected: true }) return true;
        try
        {
            return await _redis.GetDatabase().StringSetAsync("wb:cooldown:" + key, "1", ttl, When.NotExists);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            _logger.LogWarning(ex, "Redis lỗi khi chặn gửi lặp email, cho phép gửi.");
            return true;
        }
    }

    public void Dispose() => _redis?.Dispose();
}
