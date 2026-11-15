using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.TicketLookup;

/// <summary>
/// OTP tra cứu vé trên Redis. Khoá chỉ chứa hash định danh (không email/mã đặt chỗ rõ); giá trị chỉ là hash OTP + số lần thử.
/// Mọi bước đọc-sửa-ghi là Lua script nguyên tử. Redis không sẵn sàng ⇒ ném InvalidOperationException (fail closed).
/// </summary>
public sealed class RedisTicketLookupOtpStore : ITicketLookupOtpStore, IDisposable
{
    private const string Prefix = "wb:lookup:";

    // KEYS[1]=cooldown, KEYS[2]=bộ đếm theo giờ; ARGV[1]=cooldown giây, ARGV[2]=tối đa mỗi giờ.
    private const string ReserveScript = @"
if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
local n = redis.call('INCR', KEYS[2])
if n == 1 then redis.call('EXPIRE', KEYS[2], 3600) end
if n > tonumber(ARGV[2]) then return 0 end
redis.call('SET', KEYS[1], '1', 'EX', ARGV[1])
return 1";

    // KEYS[1]=otp; ARGV[1]=tối đa số lần thử. Trả {có OTP, số lần thử, hash}.
    private const string AttemptScript = @"
local h = redis.call('HGET', KEYS[1], 'h')
if not h then return {0, 0, ''} end
local a = redis.call('HINCRBY', KEYS[1], 'a', 1)
if a > tonumber(ARGV[1]) then redis.call('DEL', KEYS[1]) return {0, a, ''} end
return {1, a, h}";

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisTicketLookupOtpStore> _logger;

    public RedisTicketLookupOtpStore(IConfiguration configuration, ILogger<RedisTicketLookupOtpStore> logger)
    {
        _logger = logger;
        var connection = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";
        try
        {
            var options = ConfigurationOptions.Parse(connection);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1000;
            _redis = ConnectionMultiplexer.Connect(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không kết nối được Redis cho tra cứu vé.");
        }
    }

    public async Task<bool> TryReserveSendAsync(string identity, TimeSpan cooldown, int maxPerHour, CancellationToken ct)
    {
        var result = await Db().ScriptEvaluateAsync(ReserveScript,
            [(RedisKey)(Prefix + "cd:" + identity), (RedisKey)(Prefix + "cnt:" + identity)],
            [(RedisValue)(int)cooldown.TotalSeconds, (RedisValue)maxPerHour]);
        return (int)result == 1;
    }

    public async Task SaveAsync(string identity, string otpHash, TimeSpan ttl, CancellationToken ct)
    {
        var key = (RedisKey)(Prefix + "otp:" + identity);
        // MULTI/EXEC: OTP mới luôn bắt đầu với số lần thử 0 và TTL đầy đủ, không có trạng thái nửa chừng.
        var tx = Db().CreateTransaction();
        _ = tx.KeyDeleteAsync(key);
        _ = tx.HashSetAsync(key, [new HashEntry("h", otpHash), new HashEntry("a", 0)]);
        _ = tx.KeyExpireAsync(key, ttl);
        if (!await tx.ExecuteAsync()) throw new InvalidOperationException("Không lưu được OTP.");
    }

    public async Task<OtpAttempt> RegisterAttemptAsync(string identity, int maxAttempts, CancellationToken ct)
    {
        var result = (RedisResult[])(await Db().ScriptEvaluateAsync(AttemptScript,
            [(RedisKey)(Prefix + "otp:" + identity)], [(RedisValue)maxAttempts]))!;
        return (int)result[0] == 1
            ? new OtpAttempt((string?)result[2], (int)result[1])
            : new OtpAttempt(null, (int)result[1]);
    }

    public async Task<bool> RemoveAsync(string identity, CancellationToken ct) =>
        await Db().KeyDeleteAsync(Prefix + "otp:" + identity);

    public void Dispose() => _redis?.Dispose();

    private IDatabase Db() => _redis is { IsConnected: true }
        ? _redis.GetDatabase()
        : throw new InvalidOperationException("Redis chưa sẵn sàng.");
}
