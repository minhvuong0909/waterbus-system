using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.Tracking;

/// <summary>
/// Lưu vị trí mới nhất mỗi tàu trong 1 Redis hash (field = BoatId, value = JSON BoatPositionMessage).
/// Redis lỗi/chưa kết nối thì dùng InMemoryBoatPositionCache để realtime không bị gián đoạn.
/// </summary>
public class RedisBoatPositionCache : IBoatPositionCache, IDisposable
{
    private const string HashKey = "tracking:boats:last";

    private readonly Lazy<ConnectionMultiplexer?> _redis;
    private readonly InMemoryBoatPositionCache _fallback = new();
    private readonly ILogger<RedisBoatPositionCache> _logger;

    public RedisBoatPositionCache(IConfiguration configuration, ILogger<RedisBoatPositionCache> logger)
    {
        _logger = logger;
        var redisConn = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";

        _redis = new Lazy<ConnectionMultiplexer?>(() =>
        {
            try
            {
                var options = ConfigurationOptions.Parse(redisConn);
                options.AbortOnConnectFail = false; // tự kết nối lại khi Redis sẵn sàng
                return ConnectionMultiplexer.Connect(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không khởi tạo được kết nối Redis cho cache vị trí tàu, dùng bộ nhớ trong.");
                return null;
            }
        });
    }

    private IDatabase? Database => _redis.Value is { IsConnected: true } redis ? redis.GetDatabase() : null;

    public async Task<BoatPositionMessage?> GetAsync(Guid boatId, CancellationToken cancellationToken = default)
    {
        var db = Database;
        if (db is null) return await _fallback.GetAsync(boatId, cancellationToken);

        try
        {
            var value = await db.HashGetAsync(HashKey, boatId.ToString());
            return value.IsNullOrEmpty ? null : Deserialize(value!);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Đọc cache vị trí tàu từ Redis thất bại, dùng bộ nhớ trong.");
            return await _fallback.GetAsync(boatId, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<BoatPositionMessage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var db = Database;
        if (db is null) return await _fallback.GetAllAsync(cancellationToken);

        try
        {
            var entries = await db.HashGetAllAsync(HashKey);
            return entries
                .Select(e => Deserialize(e.Value!))
                .OfType<BoatPositionMessage>()
                .ToList();
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Đọc cache vị trí tàu từ Redis thất bại, dùng bộ nhớ trong.");
            return await _fallback.GetAllAsync(cancellationToken);
        }
    }

    public async Task SetAsync(BoatPositionMessage message, CancellationToken cancellationToken = default)
    {
        await _fallback.SetAsync(message, cancellationToken);

        var db = Database;
        if (db is null) return;

        try
        {
            await db.HashSetAsync(HashKey, message.BoatId.ToString(), JsonSerializer.Serialize(message));
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Ghi cache vị trí tàu vào Redis thất bại, chỉ giữ trong bộ nhớ trong.");
        }
    }

    private BoatPositionMessage? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BoatPositionMessage>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Bỏ qua bản ghi vị trí tàu không đọc được trong Redis.");
            return null;
        }
    }

    public void Dispose()
    {
        if (_redis.IsValueCreated) _redis.Value?.Dispose();
        GC.SuppressFinalize(this);
    }
}
