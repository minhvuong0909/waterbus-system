using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Locking;

/// <summary>
/// Triển khai Distributed Lock trên Redis bằng lệnh nguyên tử SET NX PX
/// và Lua script để giải phóng khóa an toàn (chỉ chủ sở hữu mới được xóa).
/// Đóng vai trò là Lớp Phòng Thủ 1 ngăn chặn Double-booking tại tầng RAM/Cache
/// </summary>
public class RedisDistributedLockService : IDistributedLockService, IDisposable
{
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisDistributedLockService> _logger;

    public RedisDistributedLockService(IConfiguration configuration, ILogger<RedisDistributedLockService> logger)
    {
        _logger = logger;
        var redisConn = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";

        try
        {
            _redis = ConnectionMultiplexer.Connect(redisConn);
            _logger.LogInformation("Khởi tạo kết nối Redis Distributed Lock thành công tới: {RedisEndpoint}", redisConn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối tới Redis ({RedisConnection}). Dự phòng sang lock nội bộ hoặc cảnh báo!", redisConn);
        }
    }

    public async Task<IAsyncDisposable?> AcquireLockAsync(
        string resourceKey,
        TimeSpan expiryTime,
        TimeSpan waitTime,
        TimeSpan retryTime,
        CancellationToken cancellationToken = default)
    {
        if (_redis is null || !_redis.IsConnected)
        {
            _logger.LogWarning("Redis chưa sẵn sàng! Bỏ qua Lớp 1, để Lớp 2 (RowVersion DB) xử lý.");
            return new DummyLock();
        }

        var db = _redis.GetDatabase();
        var lockToken = Guid.NewGuid().ToString("N");
        var deadline = DateTime.UtcNow + waitTime;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var acquired = await db.StringSetAsync(resourceKey, lockToken, expiryTime, When.NotExists);
            if (acquired)
            {
                _logger.LogInformation("[LOCK ACQUIRED] Lấy khóa thành công cho tài nguyên: {ResourceKey}", resourceKey);
                return new RedisLockHandle(db, resourceKey, lockToken, _logger);
            }

            if (DateTime.UtcNow >= deadline)
            {
                _logger.LogWarning("[LOCK REJECTED] Tài nguyên {ResourceKey} đang bị giữ bởi phiên khác!", resourceKey);
                return null;
            }

            await Task.Delay(retryTime, cancellationToken);
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class RedisLockHandle : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _token;
        private readonly ILogger _logger;

        public RedisLockHandle(IDatabase db, string key, string token, ILogger logger)
        {
            _db = db;
            _key = key;
            _token = token;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _db.ScriptEvaluateAsync(ReleaseScript, [_key], [_token]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi giải phóng Redis lock cho tài nguyên: {ResourceKey}", _key);
            }
        }
    }

    private sealed class DummyLock : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
