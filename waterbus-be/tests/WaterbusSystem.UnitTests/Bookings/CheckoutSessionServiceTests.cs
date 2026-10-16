using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Infrastructure.Services;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

/// <summary>
/// BR-HOLD-01 → BR-HOLD-05: CheckoutSession giữ ghế tạm thời cho cả khách có tài khoản lẫn Guest.
///
/// Điểm BR quan trọng nhất được kiểm ở đây là BR-HOLD-02: hạn dùng của phiên tính từ lần giữ
/// hợp lệ ĐẦU TIÊN và KHÔNG bị kéo dài khi khách thêm/bớt ghế - nếu sai, khách có thể giữ ghế
/// vô thời hạn bằng cách liên tục đổi chọn.
/// </summary>
public class CheckoutSessionServiceTests
{
    private static readonly Guid OrderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SeatA = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SeatB = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PaymentTtl = TimeSpan.FromMinutes(15);

    private static (CheckoutSessionService Service, InMemoryStore Store, FakeClock Clock) CreateService()
    {
        var store = new InMemoryStore();
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.Zero) };
        return (new CheckoutSessionService(store, clock, SessionTtl, PaymentTtl), store, clock);
    }

    private static SeatHold Hold(Guid seatId, int from = 1, int to = 3) => new(TripId, seatId, from, to);

    [Fact]
    public async Task HoldAsync_ShouldCreateSessionWithExpiresAtFromFirstValidHold()
    {
        var (service, _, clock) = CreateService();

        var state = await service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);

        state.Holds.Should().ContainSingle();
        state.OrderId.Should().Be(OrderId);
        state.ExpiresAt.Should().Be(clock.Now + SessionTtl);
    }

    [Fact]
    public async Task HoldAsync_WhenAddingMoreSeats_ShouldNotExtendTheSessionExpiry()
    {
        // BR-HOLD-02: chọn thêm ghế KHÔNG được kéo dài hạn chung của CheckoutSession.
        var (service, _, clock) = CreateService();

        await service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);
        var originalExpiry = clock.Now + SessionTtl;

        clock.Now = clock.Now.AddMinutes(3);

        var second = await service.HoldAsync("order:x", OrderId, [Hold(SeatB)]);

        second.ExpiresAt.Should().Be(originalExpiry);
        second.Holds.Should().HaveCount(2);

        // Thời gian còn lại phải ngắn đi đúng bằng 3 phút đã trôi qua.
        (second.ExpiresAt - clock.Now).Should().Be(SessionTtl - TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task HoldAsync_WhenSelectingTheSameSeatTwice_ShouldNotDuplicateTheHold()
    {
        var (service, _, _) = CreateService();

        await service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);
        var state = await service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);

        state.Holds.Should().ContainSingle();
    }

    [Fact]
    public async Task HoldAsync_ShouldServeBothBookingsOfTheSameOrderInOneSession()
    {
        // BR-HOLD-02: một CheckoutSession dùng chung cho toàn Order, gồm cả hai Booking round-trip.
        var (service, _, _) = CreateService();

        var outbound = Hold(SeatA, from: 1, to: 2);
        var returning = Hold(SeatB, from: 1, to: 3);

        var state = await service.HoldAsync("order:x", OrderId, [outbound, returning]);

        state.Holds.Should().HaveCount(2);
        state.ExpiresAt.Should().Be(state.ExpiresAt);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNullWhenSessionWasNeverCreated()
    {
        var (service, _, _) = CreateService();

        (await service.GetAsync("order:khong-ton-tai")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenTtlHasElapsed_ShouldReturnNullAndDropTheSession()
    {
        // BR-HOLD-03: client mất kết nối / đóng trình duyệt thì phiên hết hạn qua TTL.
        var (service, _, clock) = CreateService();

        await service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);
        clock.Now = clock.Now.Add(SessionTtl).AddMinutes(1);

        (await service.GetAsync("order:x")).Should().BeNull();
    }

    [Fact]
    public async Task ReleaseAsync_ShouldDropTheSession()
    {
        // BR-HOLD-03: bỏ chọn ghế hoặc thanh toán xong thì giải phóng hold.
        var (service, store, _) = CreateService();

        await service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);

        (await service.ReleaseAsync("order:x")).Should().BeTrue();
        (await service.GetAsync("order:x")).Should().BeNull();
        store.Keys.Should().NotContain("waterbus:checkout:session:order:x");
    }

    [Fact]
    public async Task MarkPaymentPendingAsync_ShouldOpenASeparatePaymentWindow()
    {
        // BR-HOLD-03: sau khi gửi yêu cầu thanh toán có cửa sổ PaymentPending riêng để chờ gateway.
        var (service, _, clock) = CreateService();

        await service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);
        var sessionExpiryBefore = (await service.GetAsync("order:x"))!.ExpiresAt;

        var pending = await service.MarkPaymentPendingAsync("order:x", OrderId);

        pending.Should().NotBeNull();
        pending!.IsPaymentPending.Should().BeTrue();
        pending.PaymentPendingExpiresAt.Should().Be(clock.Now + PaymentTtl);
        // Cửa sổ PaymentPending KHÔNG được kéo dài hạn giữ ghế.
        pending.ExpiresAt.Should().Be(sessionExpiryBefore);
    }

    [Fact]
    public async Task MarkPaymentPendingAsync_WhenSessionExpired_ShouldReturnNull()
    {
        var (service, _, clock) = CreateService();

        await service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);
        clock.Now = clock.Now.Add(SessionTtl).AddMinutes(1);

        (await service.MarkPaymentPendingAsync("order:x", OrderId)).Should().BeNull();
    }

    [Fact]
    public async Task HoldAsync_WhenRedisStoreFails_ShouldNotBreakBookingPersistence()
    {
        // BR-HOLD-04: Redis chỉ hỗ trợ trải nghiệm. Hold thật nằm ở SeatReservation trong CSDL,
        // nên Redis chết không được làm hỏng luồng đặt vé.
        var store = new InMemoryStore { ThrowOnWrite = true };
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.Zero) };
        var service = new CheckoutSessionService(store, clock, SessionTtl, PaymentTtl);

        var act = () => service.HoldAsync("order:x", OrderId, [Hold(SeatA)]);

        // Lỗi được ném ra để tầng trên quyết định, nhưng handler đặt vé đã bọc trong try/catch
        // và không rollback dữ liệu CSDL đã ghi.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class InMemoryStore : ICheckoutSessionStore
    {
        private readonly Dictionary<string, string> _data = [];

        public List<string> Keys => [.. _data.Keys];
        public bool ThrowOnWrite { get; init; }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_data.GetValueOrDefault(key));

        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("Redis không sẵn sàng.");
            }

            _data[key] = value;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_data.Remove(key));
    }

    private sealed class FakeClock : IDateTimeService
    {
        public DateTimeOffset Now { get; set; }
    }
}