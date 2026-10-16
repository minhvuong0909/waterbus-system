using System.Text.Json;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Bookings.Services;

/// <summary>
/// BR-HOLD-01 → BR-HOLD-05: quản lý phiên giữ ghế tạm thời (CheckoutSession).
///
/// Ràng buộc quan trọng nhất - BR-HOLD-02:
///   • Một CheckoutSession dùng CHUNG cho toàn Order, gồm cả hai Booking của round-trip.
///   • Hạn dùng chung tính từ lần giữ hợp lệ ĐẦU TIÊN; thêm/bớt ghế KHÔNG được kéo dài hạn.
///
/// BR-HOLD-04: Redis chỉ giúp tọa độ hiển thị và đếm ngược. Tính nhất quán thật sự khi commit
/// vẫn phải do Segment Overlap trên SeatReservation + RowVersion bảo đảm ở tầng CSDL.
/// </summary>
public class CheckoutSessionService : ICheckoutSessionService
{
    private const string KeyPrefix = "waterbus:checkout:session:";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ICheckoutSessionStore _store;
    private readonly IDateTimeService _clock;
    private readonly TimeSpan _sessionTtl;
    private readonly TimeSpan _paymentPendingTtl;

    /// <summary>
    /// TTL do tầng Infrastructure đọc từ cấu hình rồi truyền vào, để tầng Application không phụ
    /// thuộc vào hệ thống cấu hình và hành vi BR-HOLD-02 kiểm thử được với thời gian xác định.
    /// </summary>
    public CheckoutSessionService(
        ICheckoutSessionStore store,
        IDateTimeService clock,
        TimeSpan sessionTtl,
        TimeSpan paymentPendingTtl)
    {
        _store = store;
        _clock = clock;
        _sessionTtl = sessionTtl > TimeSpan.Zero ? sessionTtl : TimeSpan.FromMinutes(10);
        _paymentPendingTtl = paymentPendingTtl > TimeSpan.Zero ? paymentPendingTtl : TimeSpan.FromMinutes(15);
    }

    public async Task<CheckoutSessionState> HoldAsync(
        string sessionId, Guid orderId, IReadOnlyList<SeatHold> holds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var now = _clock.Now;
        var existing = await GetAsync(sessionId, cancellationToken);

        // BR-HOLD-02: nếu phiên đã tồn tại thì GIỮ NGUYÊN mốc hết hạn gốc, chỉ gia hạn key theo
        // thời gian còn lại. Tuyệt đối không đặt lại thành now + TTL.
        var expiresAt = existing?.ExpiresAt ?? now.Add(_sessionTtl);
        var remaining = expiresAt - now;

        if (remaining <= TimeSpan.Zero)
        {
            // Phiên đã hết hạn ở phía server dù client vẫn giữ state cũ.
            remaining = _sessionTtl;
            expiresAt = now.Add(remaining);
        }

        // Gộp theo (Trip, Seat, đoạn) để chọn trùng ghế không tạo bản ghi giữ trùng lặp.
        var merged = (existing?.Holds ?? [])
            .Concat(holds)
            .Distinct()
            .ToList();

        var state = new CheckoutSessionState
        {
            SessionId = sessionId,
            OrderId = orderId == Guid.Empty ? existing?.OrderId ?? orderId : orderId,
            Holds = merged,
            ExpiresAt = expiresAt,
            IsPaymentPending = existing?.IsPaymentPending ?? false,
            PaymentPendingExpiresAt = existing?.PaymentPendingExpiresAt
        };

        await _store.SetAsync(Key(sessionId), JsonSerializer.Serialize(state, SerializerOptions), remaining, cancellationToken);

        return state;
    }

    public async Task<CheckoutSessionState> HoldUntilAsync(string sessionId, Guid orderId,
        IReadOnlyList<SeatHold> holds, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        if (expiresAt <= _clock.Now) throw new InvalidOperationException("Checkout expired.");
        var state = new CheckoutSessionState { SessionId = sessionId, OrderId = orderId,
            Holds = holds.Distinct().ToList(), ExpiresAt = expiresAt };
        await _store.SetAsync(Key(sessionId), JsonSerializer.Serialize(state, SerializerOptions), expiresAt - _clock.Now, cancellationToken);
        return state;
    }

    public async Task<CheckoutSessionState?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var json = await _store.GetAsync(Key(sessionId), cancellationToken);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var state = JsonSerializer.Deserialize<CheckoutSessionState>(json, SerializerOptions);
        if (state == null)
        {
            return null;
        }

        // Phòng vệ lớp TTL: kho có thể trả về giá trị cũ nếu cấu hình TTL vừa bị rút ngắn.
        if ((state.IsPaymentPending ? state.PaymentPendingExpiresAt ?? state.ExpiresAt : state.ExpiresAt) <= _clock.Now)
        {
            await _store.RemoveAsync(Key(sessionId), cancellationToken);
            return null;
        }

        return state;
    }

    public Task<bool> ReleaseAsync(string sessionId, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(sessionId)
            ? Task.FromResult(false)
            : _store.RemoveAsync(Key(sessionId), cancellationToken);

    public async Task<CheckoutSessionState?> MarkPaymentPendingUntilAsync(string sessionId, Guid orderId,
        DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(sessionId, cancellationToken);
        if (existing is null || expiresAt <= _clock.Now) return null;
        var state = existing with { OrderId = orderId, IsPaymentPending = true, PaymentPendingExpiresAt = expiresAt };
        await _store.SetAsync(Key(sessionId), JsonSerializer.Serialize(state, SerializerOptions), expiresAt - _clock.Now, cancellationToken);
        return state;
    }

    public async Task<CheckoutSessionState?> MarkPaymentPendingAsync(
        string sessionId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(sessionId, cancellationToken);
        if (existing == null)
        {
            return null;
        }

        if (existing.IsPaymentPending) return existing;
        var now = _clock.Now;
        var state = existing with
        {
            OrderId = orderId == Guid.Empty ? existing.OrderId : orderId,
            IsPaymentPending = true,
            PaymentPendingExpiresAt = now.Add(_paymentPendingTtl)
        };

        // Cửa sổ chờ payment riêng, chỉ mở một lần; giữ state đến hết cửa sổ này.
        var remaining = state.PaymentPendingExpiresAt!.Value - now;
        if (remaining <= TimeSpan.Zero)
        {
            return null;
        }

        await _store.SetAsync(Key(sessionId), JsonSerializer.Serialize(state, SerializerOptions), remaining, cancellationToken);

        return state;
    }

    private static string Key(string sessionId) => KeyPrefix + sessionId;
}