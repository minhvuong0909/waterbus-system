using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.Checkout;

/// <summary>
/// BR-HOLD-04: Redis (hoặc cache tương đương) hỗ trợ temporary holds. Lớp này chỉ lo phần
/// I/O với TTL; mọi quy tắc nghiệp vụ nằm ở CheckoutSessionService để kiểm thử được.
///
/// Lưu ý: nếu Redis không sẵn sàng, hệ thống KHÔNG được âm thầm coi như đã giữ ghế. Hold thật
/// sự vẫn nằm ở SeatReservation trong SQL Server, nên mất Redis chỉ ảnh hưởng trải nghiệm
/// đếm ngược, không ảnh hưởng tính nhất quán khi commit.
/// </summary>
public class RedisCheckoutSessionStore : ICheckoutSessionStore, IDisposable
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCheckoutSessionStore> _logger;

    public RedisCheckoutSessionStore(IConfiguration configuration, ILogger<RedisCheckoutSessionStore> logger)
    {
        _logger = logger;
        var redisConn = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";

        try
        {
            var options = ConfigurationOptions.Parse(redisConn);
            options.AbortOnConnectFail = false; options.ConnectTimeout = 1000;
            _redis = ConnectionMultiplexer.Connect(options);
            _logger.LogInformation("Khởi tạo kết nối Redis cho CheckoutSession.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không kết nối được Redis cho CheckoutSession.");
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        var value = await _redis!.GetDatabase().StringGetAsync(key);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || ttl <= TimeSpan.Zero)
        {
            return;
        }

        // BR-HOLD-02: TTL truyền vào luôn là thời gian CÒN LẠI của phiên, không phải TTL đầy đủ.
        await _redis!.GetDatabase().StringSetAsync(key, value, ttl, When.Always);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return false;
        }

        return await _redis!.GetDatabase().KeyDeleteAsync(key);
    }

    public void Dispose() => _redis?.Dispose();

    private bool IsAvailable => _redis is { IsConnected: true };
}