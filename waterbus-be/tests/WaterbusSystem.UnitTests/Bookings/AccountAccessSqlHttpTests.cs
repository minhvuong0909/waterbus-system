using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using WaterbusSystem.Application;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.WebApi.Middlewares;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

/// <summary>
/// Task 4 — Guest / Account / Ticket access. Host HTTP thật (Kestrel + JWT) trên SQL Server thật; chỉ Redis được thay bằng fake.
/// Phủ: claim bằng token (không theo email), phân quyền chéo tài khoản, claim đồng thời, replay/rotate/revoke/hết hạn,
/// TripAccess không lộ tiền, OneWay/RoundTrip, rollback giao dịch.
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class AccountAccessSqlHttpTests : IAsyncLifetime
{
    private string? _connection;
    private WebApplication _app = null!;
    private readonly string _jwtSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured)
        { InitialCatalog = "WaterbusDomainTests_Access_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using (var c = Context()) await c.Database.MigrateAsync();

        using var signingKey = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connection,
            ["JwtSettings:Secret"] = _jwtSecret,
            ["VnPay:TmnCode"] = "ACCTEST01",
            ["VnPay:HashSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Qr:PrivateKeyPem"] = signingKey.ExportPkcs8PrivateKeyPem()
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddSingleton<IDistributedLockService, FakeDistributedLockService>();
        builder.Services.AddSingleton<ICheckoutSessionStore, CommercialHandlerFactory.MemoryStore>();
        builder.Services.AddScoped<ValidateRequestFilter>();
        builder.Services.AddControllers().AddApplicationPart(typeof(BookingsController).Assembly);
        builder.Services.AddRateLimiter(o =>
        {
            o.AddPolicy("BookingPolicy", _ => RateLimitPartition.GetNoLimiter("t"));
        });
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseAuthentication();
        _app.UseMiddleware<GuestAccessMiddleware>();
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.MapControllers();
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_Access_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context();
        await c.Database.EnsureDeletedAsync();
    }

    // ───────────────────────── fixtures ─────────────────────────

    private sealed record Seeded(TestDbContextFactory.TripSeedData Seed, Guid OrderId, Guid BookingId, Guid TicketId, string Token);

    private HttpClient Client(Guid? accountId = null, string role = "Passenger", string? guestToken = null, string? email = null)
    {
        var client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };
        if (accountId is { } id)
        {
            var jwt = new JwtSecurityToken("WaterbusSystemApi", "WaterbusClients",
                [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role),
                    .. (email is null ? Array.Empty<Claim>() : [new Claim(ClaimTypes.Email, email)])],
                expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }
        if (guestToken is not null) client.DefaultRequestHeaders.Add("X-Guest-Token", guestToken);
        return client;
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static AccessGrant Grant(Guid orderId, string token, string type = "ManageOrder", Guid? bookingId = null,
        DateTimeOffset? expires = null) => new()
    {
        OrderId = orderId, BookingId = bookingId, AccessType = type, TokenHash = CheckoutAccessService.Hash(token),
        ExpiresAt = expires ?? DateTimeOffset.UtcNow.AddDays(1)
    };

    /// <summary>Order Confirmed đã phát hành Ticket (một chiều) kèm một Manage Order token.</summary>
    private async Task<Seeded> SeedPaidOrderAsync(Guid? accountId = null, string email = "khach@test.com")
    {
        await using var c = Context();
        var scenario = CheckInScenarioFactory.SeedCheckInScenario(c, Guid.NewGuid(), withAssignment: false);
        scenario.Booking.Order!.PassengerAccountId = accountId;
        scenario.Booking.Order.PurchaserEmail = email;
        scenario.Booking.PaymentStatus = PaymentStatus.Success;
        var token = NewToken();
        c.AccessGrants.Add(Grant(scenario.Booking.OrderId, token));
        await c.SaveChangesAsync();
        return new Seeded(scenario.Seed, scenario.Booking.OrderId, scenario.Booking.Id, scenario.Ticket.Id, token);
    }

    /// <summary>Thêm Order Confirmed thứ hai (ghế 2) cho cùng chuyến, trả OrderId/BookingId/TicketId/Token.</summary>
    private async Task<Seeded> AddPaidOrderAsync(Seeded first, Guid? accountId = null)
    {
        await using var c = Context();
        var boarding = await c.TripStopCalls.SingleAsync(x => x.TripId == first.Seed.Trip.Id && x.VisitOrder == 1);
        var disembark = await c.TripStopCalls.SingleAsync(x => x.TripId == first.Seed.Trip.Id && x.VisitOrder == 3);
        var (order, booking, ticket) = BuildPaid(c, first.Seed, boarding, disembark, first.Seed.Seat2, accountId, null);
        var token = NewToken();
        c.AccessGrants.Add(Grant(order.Id, token));
        await c.SaveChangesAsync();
        return new Seeded(first.Seed, order.Id, booking.Id, ticket.Id, token);
    }

    private static (PurchaseOrder, Booking, Ticket) BuildPaid(ApplicationDbContext c, TestDbContextFactory.TripSeedData seed,
        TripStopCall boarding, TripStopCall disembark, Seat seat, Guid? accountId, PurchaseOrder? shared)
    {
        var fare = TestDbContextFactory.StandardCommuterFare;
        var order = shared ?? new PurchaseOrder
        {
            PurchaserName = "Khach Hai", PurchaserEmail = "khach2@test.com", QuotedTotal = fare,
            Status = OrderStatus.Confirmed, PassengerAccountId = accountId
        };
        var booking = new Booking
        {
            Order = order, OrderId = order.Id, BookingCode = Guid.NewGuid().ToString("N"),
            CustomerName = order.PurchaserName, CustomerEmail = order.PurchaserEmail, QuotedTotal = fare,
            Status = BookingStatus.Confirmed, PaymentStatus = PaymentStatus.Success, PaidAllocation = fare
        };
        booking.SetJourney(boarding, disembark);
        var reservation = new SeatReservation
        {
            TripId = seed.Trip.Id, SeatId = seat.Id, BookingId = booking.Id, Booking = booking,
            BoardingStopOrder = boarding.VisitOrder, DisembarkingStopOrder = disembark.VisitOrder,
            Status = ReservationStatus.Confirmed, PassengerName = "Hanh Khach " + seat.SeatCode,
            QuotedFare = fare, PaidAllocation = fare, SeatCodeAtSale = seat.SeatCode, SeatClassAtSale = seed.StandardClass.Name
        };
        booking.SeatReservations.Add(reservation);
        var payment = new FinancialTransaction
        {
            OrderId = order.Id, Order = order, IdempotencyKey = "idem-" + Guid.NewGuid(),
            Status = FinancialTransactionStatus.Succeeded, Amount = fare
        };
        var ticket = Ticket.Issue(booking, reservation, payment, TripType.Commuter, DateTimeOffset.UtcNow);
        booking.Tickets.Add(ticket);
        if (shared is null) c.PurchaseOrders.Add(order);
        c.Bookings.Add(booking);
        c.SeatReservations.Add(reservation);
        c.FinancialTransactions.Add(payment);
        c.Tickets.Add(ticket);
        return (order, booking, ticket);
    }

    private async Task<Guid?> OwnerOf(Guid orderId)
    {
        await using var c = Context();
        return (await c.PurchaseOrders.AsNoTracking().SingleAsync(o => o.Id == orderId)).PassengerAccountId;
    }

    private static Task<HttpResponseMessage> Post(HttpClient c, string url) => c.PostAsync(url, null);

    // ───────────────────────── Claim (BR-ID-04) ─────────────────────────

    [SqlServerDomainFact]
    public async Task Claim_RequiresPassengerLoginAndValidTokenOfThatOrder_AndNeverUsesEmailMatch()
    {
        var a = await SeedPaidOrderAsync(email: "passenger@test.com");
        var account = Guid.NewGuid();
        var url = $"/api/v1/checkouts/{a.OrderId}/claim";

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(Client(guestToken: a.Token), url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(account, "Staff", a.Token), url)).StatusCode);
        // Đăng nhập bằng JWT mang đúng email của Purchaser nhưng không có token: KHÔNG được gán, không đọc được đơn.
        var sameEmail = Client(account, email: "passenger@test.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(sameEmail, url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sameEmail.GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        using (var history = JsonDocument.Parse(await sameEmail.GetStringAsync("/api/v1/me/orders")))
            Assert.Equal(0, history.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(account, guestToken: "not-a-token"), url)).StatusCode);
        var other = await AddPaidOrderAsync(a);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(account, guestToken: other.Token), url)).StatusCode);
        Assert.Null(await OwnerOf(a.OrderId));

        var ok = await Post(Client(account, guestToken: a.Token), url);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = (await ok.Content.ReadFromJsonAsync<ClaimOrderResponse>())!;
        Assert.False(body.AlreadyLinked);
        Assert.Equal(account, await OwnerOf(a.OrderId));
    }

    [SqlServerDomainFact]
    public async Task Claim_IsIdempotentForSameAccount_AndConflictsForAnotherAccount()
    {
        var a = await SeedPaidOrderAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var url = $"/api/v1/checkouts/{a.OrderId}/claim";

        Assert.Equal(HttpStatusCode.OK, (await Post(Client(first, guestToken: a.Token), url)).StatusCode);
        var repeat = await Post(Client(first, guestToken: a.Token), url);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.True((await repeat.Content.ReadFromJsonAsync<ClaimOrderResponse>())!.AlreadyLinked);
        var steal = await Post(Client(second, guestToken: a.Token), url);
        Assert.Equal(HttpStatusCode.Conflict, steal.StatusCode);
        Assert.Equal(first, await OwnerOf(a.OrderId));
    }

    [SqlServerDomainFact]
    public async Task ConcurrentClaims_ByDifferentAccounts_HaveExactlyOneWinner()
    {
        var a = await SeedPaidOrderAsync();
        var accounts = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var url = $"/api/v1/checkouts/{a.OrderId}/claim";
        var results = await Task.WhenAll(accounts.Select(id => Task.Run(async () =>
            (id, (await Post(Client(id, guestToken: a.Token), url)).StatusCode))));
        var winners = results.Where(r => r.Item2 == HttpStatusCode.OK).ToList();
        Assert.Single(winners);
        Assert.Equal(5, results.Count(r => r.Item2 == HttpStatusCode.Conflict));
        Assert.Equal(winners[0].id, await OwnerOf(a.OrderId));
    }

    [SqlServerDomainFact]
    public async Task ConcurrentClaims_BySameAccount_AllSucceedWithSingleLink()
    {
        var a = await SeedPaidOrderAsync();
        var account = Guid.NewGuid();
        var url = $"/api/v1/checkouts/{a.OrderId}/claim";
        var codes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
            (await Post(Client(account, guestToken: a.Token), url)).StatusCode)));
        Assert.All(codes, c => Assert.Equal(HttpStatusCode.OK, c));
        Assert.Equal(account, await OwnerOf(a.OrderId));
    }

    [SqlServerDomainFact]
    public async Task Claim_RollsBackWhenTheTransactionFailsAfterSave()
    {
        var a = await SeedPaidOrderAsync();
        await using var c = Context();
        var service = Service(c, new FailAfterSaveUnitOfWork(new EfUnitOfWork(c)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ClaimAsync(a.OrderId, Guid.NewGuid(), a.Token));
        Assert.Null(await OwnerOf(a.OrderId));
    }

    [SqlServerDomainFact]
    public async Task Rotate_RollsBackWhenTheTransactionFailsAfterSave_KeepingTheOldTokenValid()
    {
        var a = await SeedPaidOrderAsync();
        await using (var c = Context())
        {
            var service = Service(c, new FailAfterSaveUnitOfWork(new EfUnitOfWork(c)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(a.OrderId, a.Token));
        }
        await using var check = Context();
        Assert.Equal(1, await check.AccessGrants.CountAsync(g => g.OrderId == a.OrderId));
        Assert.Null((await check.AccessGrants.SingleAsync(g => g.OrderId == a.OrderId)).RevokedAt);
        Assert.Equal(HttpStatusCode.OK, (await Client(guestToken: a.Token).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
    }

    private static OrderAccessService Service(ApplicationDbContext c, IUnitOfWork uow) => new(
        new CheckoutAccessService(new AccessGrantRepository(c), new CheckoutOptions()), new AccessGrantRepository(c),
        new OrderRepository(c), new AccountAccessRepository(c), new SqlCommercialTransactionRunner(c), uow,
        NullLogger<OrderAccessService>.Instance);

    private sealed class FailAfterSaveUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await inner.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("injected failure after flush");
        }
        public void DiscardTrackedChanges() => inner.DiscardTrackedChanges();
    }

    // ───────────────────── Cross-account isolation & history ─────────────────────

    [SqlServerDomainFact]
    public async Task OwnerSeesOnlyOwnOrdersAndTickets_OtherPassengerAndAnonymousAreDenied()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var a = await SeedPaidOrderAsync(alice);
        var b = await AddPaidOrderAsync(a, bob);

        var mine = await Client(alice).GetAsync("/api/v1/me/orders?pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        using var json = JsonDocument.Parse(await mine.Content.ReadAsStringAsync());
        var items = json.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(a.OrderId, items[0].GetProperty("orderId").GetGuid());
        Assert.Equal("Confirmed", items[0].GetProperty("orderStatus").GetString());
        Assert.Equal("Success", items[0].GetProperty("bookings")[0].GetProperty("paymentStatus").GetString());

        // vé của người khác: 404 (không lộ sự tồn tại); vé của mình: 200 với 4 trục trạng thái tách riêng
        Assert.Equal(HttpStatusCode.NotFound, (await Client(alice).GetAsync($"/api/v1/me/tickets/{b.TicketId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client(bob).GetAsync($"/api/v1/me/tickets/{a.TicketId}")).StatusCode);
        var ticket = (await Client(alice).GetFromJsonAsync<TicketDetailDto>($"/api/v1/me/tickets/{a.TicketId}"))!;
        Assert.Equal(("Confirmed", "Success", "Valid", "NotCheckedIn", true),
            (ticket.BookingStatus, ticket.PaymentStatus, ticket.TicketStatus, ticket.BoardingStatus, ticket.ServiceEntitled));
        Assert.Equal("Ben A", ticket.BoardingStation);
        Assert.Equal("Ben C", ticket.DisembarkingStation);

        // truy cập Order bằng route checkouts: chủ sở hữu không cần token, người khác/ẩn danh bị 403
        Assert.Equal(HttpStatusCode.OK, (await Client(alice).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(alice).GetAsync($"/api/v1/checkouts/{a.OrderId}/bookings/{a.BookingId}/qr")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(alice).GetAsync($"/api/v1/checkouts/{a.OrderId}/tickets/{a.TicketId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(bob).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(bob).GetAsync($"/api/v1/checkouts/{a.OrderId}/bookings/{a.BookingId}/qr")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client().GetAsync($"/api/v1/checkouts/{a.OrderId}/tickets/{a.TicketId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(bob).DeleteAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/api/v1/me/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Guid.NewGuid(), "Staff").GetAsync("/api/v1/me/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client(alice).GetAsync("/api/v1/me/orders?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client(alice).GetAsync("/api/v1/me/orders?pageSize=101")).StatusCode);
    }

    [SqlServerDomainFact]
    public async Task GuestTicketAccess_IsScopedToTheOrderOfTheToken()
    {
        var a = await SeedPaidOrderAsync();
        var b = await AddPaidOrderAsync(a);
        var own = await Client(guestToken: a.Token).GetAsync($"/api/v1/checkouts/{a.OrderId}/tickets/{a.TicketId}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        // token của A + ticket của B (cùng orderId của A) → 404; orderId của B với token A → 403
        Assert.Equal(HttpStatusCode.NotFound, (await Client(guestToken: a.Token).GetAsync($"/api/v1/checkouts/{a.OrderId}/tickets/{b.TicketId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(guestToken: a.Token).GetAsync($"/api/v1/checkouts/{b.OrderId}/tickets/{b.TicketId}")).StatusCode);
    }

    [SqlServerDomainFact]
    public async Task RoundTripOrder_ClaimLinksBothLegsAndHistoryListsOneOrderWithTwoBookings()
    {
        var a = await SeedPaidOrderAsync();
        await using (var c = Context())
        {
            var order = await c.PurchaseOrders.SingleAsync(o => o.Id == a.OrderId);
            order.PurchaseMode = PurchaseMode.RoundTrip;
            var boarding = await c.TripStopCalls.SingleAsync(x => x.TripId == a.Seed.Trip.Id && x.VisitOrder == 1);
            var disembark = await c.TripStopCalls.SingleAsync(x => x.TripId == a.Seed.Trip.Id && x.VisitOrder == 3);
            BuildPaid(c, a.Seed, boarding, disembark, a.Seed.Seat2, null, order);
            await c.SaveChangesAsync();
        }
        var account = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(Client(account, guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/claim")).StatusCode);
        using var json = JsonDocument.Parse(await Client(account).GetStringAsync("/api/v1/me/orders"));
        var orders = json.RootElement.GetProperty("items");
        Assert.Equal(1, orders.GetArrayLength());
        Assert.Equal("RoundTrip", orders[0].GetProperty("purchaseMode").GetString());
        Assert.Equal(2, orders[0].GetProperty("bookings").GetArrayLength());
    }

    [SqlServerDomainFact]
    public async Task PassengerCheckout_IsLinkedToTheAccountImmediately_GuestCheckoutIsNot()
    {
        await using var c = Context();
        var seed = TestDbContextFactory.SeedTrip(c);
        var account = Guid.NewGuid();
        object Body(Guid seat) => new { tripId = seed.Trip.Id, seatIds = new[] { seat }, boardingStationId = seed.StationA.Id,
            disembarkingStationId = seed.StationC.Id, customerName = "Buyer", customerEmail = "buyer@example.com" };
        var signedIn = await Client(account).PostAsJsonAsync("/api/v1/bookings", Body(seed.Seat1.Id));
        Assert.Equal(HttpStatusCode.Created, signedIn.StatusCode);
        var guest = await Client().PostAsJsonAsync("/api/v1/bookings", Body(seed.Seat2.Id));
        Assert.Equal(HttpStatusCode.Created, guest.StatusCode);
        Assert.Equal(account, await OwnerOf((await signedIn.Content.ReadFromJsonAsync<BookingResponse>())!.OrderId));
        Assert.Null(await OwnerOf((await guest.Content.ReadFromJsonAsync<BookingResponse>())!.OrderId));
        // chủ sở hữu xem đơn đang giữ chỗ nhưng lịch sử không liệt kê Draft; PendingPayment thì có
        using var json = JsonDocument.Parse(await Client(account).GetStringAsync("/api/v1/me/orders"));
        Assert.Equal(1, json.RootElement.GetProperty("items").GetArrayLength());
    }

    // ───────────────────── Token lifecycle (BR-ID-02/07) ─────────────────────

    [SqlServerDomainFact]
    public async Task Rotate_RevokesTheOldToken_SoReplayIsRejected()
    {
        var a = await SeedPaidOrderAsync();
        var rotate = await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/access/rotate");
        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);
        var fresh = (await rotate.Content.ReadFromJsonAsync<AccessTokenResponse>())!;
        Assert.NotEqual(a.Token, fresh.AccessToken);
        Assert.True(fresh.AccessToken.Length >= 64);

        Assert.Equal(HttpStatusCode.Forbidden, (await Client(guestToken: a.Token).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/access/rotate")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(Guid.NewGuid(), guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/claim")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(guestToken: fresh.AccessToken).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);

        // chỉ lưu hash, không lưu token gốc
        await using var c = Context();
        var hashes = await c.AccessGrants.Select(g => g.TokenHash).ToListAsync();
        Assert.DoesNotContain(fresh.AccessToken, hashes);
        Assert.Contains(CheckoutAccessService.Hash(fresh.AccessToken), hashes);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentRotationsWithTheSameToken_HaveExactlyOneWinner()
    {
        var a = await SeedPaidOrderAsync();
        var codes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
            (await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/access/rotate")).StatusCode)));
        Assert.Equal(1, codes.Count(x => x == HttpStatusCode.OK));
        Assert.Equal(4, codes.Count(x => x == HttpStatusCode.Forbidden));
        await using var c = Context();
        Assert.Equal(1, await c.AccessGrants.CountAsync(g => g.OrderId == a.OrderId && g.RevokedAt == null));
    }

    [SqlServerDomainFact]
    public async Task OwnerCanRotateWithoutOldToken_AndOldLinksDie_AnonymousAndStrangersCannot()
    {
        var owner = Guid.NewGuid();
        var a = await SeedPaidOrderAsync(owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(), $"/api/v1/checkouts/{a.OrderId}/access/rotate")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(Guid.NewGuid()), $"/api/v1/checkouts/{a.OrderId}/access/rotate")).StatusCode);
        var rotated = await Post(Client(owner), $"/api/v1/checkouts/{a.OrderId}/access/rotate");
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(guestToken: a.Token).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
    }

    [SqlServerDomainFact]
    public async Task ExpiredAndRevokedTokensAreRejected_RevokeKillsTripAccessToo()
    {
        var a = await SeedPaidOrderAsync();
        var expired = NewToken();
        var tripToken = NewToken();
        await using (var c = Context())
        {
            c.AccessGrants.Add(Grant(a.OrderId, expired, expires: DateTimeOffset.UtcNow.AddMinutes(-1)));
            c.AccessGrants.Add(Grant(a.OrderId, tripToken, "PassengerTrip", a.BookingId));
            await c.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(guestToken: expired).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TripAccess(tripToken)).StatusCode);

        var revoked = await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/access/revoke");
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(2, (await revoked.Content.ReadFromJsonAsync<RevokeAccessResponse>())!.RevokedCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(guestToken: a.Token).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TripAccess(tripToken)).StatusCode);
    }

    [SqlServerDomainFact]
    public async Task MutationsWithARevokedToken_AreRejectedAndLeaveTheOrderUntouched()
    {
        var a = await SeedPaidOrderAsync();
        Assert.Equal(HttpStatusCode.OK, (await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/access/revoke")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(guestToken: a.Token).DeleteAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/access/rotate")).StatusCode);
        await using var c = Context();
        Assert.Equal(OrderStatus.Confirmed, (await c.PurchaseOrders.AsNoTracking().SingleAsync(o => o.Id == a.OrderId)).Status);
    }

    // ───────────────────── TripAccess (BR-ID-05/06) ─────────────────────

    private Task<HttpResponseMessage> TripAccess(string? token)
    {
        var client = Client();
        if (token is not null) client.DefaultRequestHeaders.Add("X-Trip-Access-Token", token);
        return client.GetAsync("/api/v1/tripaccess");
    }

    [SqlServerDomainFact]
    public async Task TripAccess_ShowsJourneyOnly_NeverPaymentPurchaserQrOrOrderAccess()
    {
        var a = await SeedPaidOrderAsync(email: "secret-purchaser@test.com");
        var issued = await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/bookings/{a.BookingId}/trip-access");
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var link = (await issued.Content.ReadFromJsonAsync<AccessTokenResponse>())!.AccessToken;

        var response = await TripAccess(link);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("Ben A", json.RootElement.GetProperty("boardingStation").GetString());
        Assert.Equal("Nguyen Van A", json.RootElement.GetProperty("tickets")[0].GetProperty("passengerName").GetString());
        foreach (var forbidden in new[] { "paidFare", "quotedTotal", "amount", "payment", "secret-purchaser", "qr", "orderId", "ticketCode", "accessToken" })
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);

        // TripAccess token không mở được Order; Manage Order token không mở được TripAccess; thiếu/sai token → 403
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(guestToken: link).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(guestToken: link).GetAsync($"/api/v1/checkouts/{a.OrderId}/bookings/{a.BookingId}/qr")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TripAccess(a.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TripAccess(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TripAccess("nope")).StatusCode);
    }

    [SqlServerDomainFact]
    public async Task TripAccess_IssuingRequiresAPaidBookingOfTheSameOrderAndAnAuthorizedCaller()
    {
        var a = await SeedPaidOrderAsync();
        var b = await AddPaidOrderAsync(a);
        // Booking của order khác → 404; không token → 403; Booking chưa thanh toán → 409
        Assert.Equal(HttpStatusCode.NotFound, (await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/bookings/{b.BookingId}/trip-access")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Client(), $"/api/v1/checkouts/{a.OrderId}/bookings/{a.BookingId}/trip-access")).StatusCode);
        await using (var c = Context())
        {
            (await c.Bookings.SingleAsync(x => x.Id == a.BookingId)).Status = BookingStatus.PendingPayment;
            await c.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Post(Client(guestToken: a.Token), $"/api/v1/checkouts/{a.OrderId}/bookings/{a.BookingId}/trip-access")).StatusCode);
    }

    // ───────────────────── Existing Guest behaviour is preserved ─────────────────────

    [SqlServerDomainFact]
    public async Task GuestWithTokenStillReadsOrderAndQr_AndLegacyAnonymousStillDenied()
    {
        var a = await SeedPaidOrderAsync();
        var detail = await Client(guestToken: a.Token).GetFromJsonAsync<CheckoutDetailDto>($"/api/v1/checkouts/{a.OrderId}");
        Assert.Equal(a.OrderId, detail!.OrderId);
        Assert.Equal(HttpStatusCode.OK, (await Client(guestToken: a.Token).GetAsync($"/api/v1/checkouts/{a.OrderId}/bookings/{a.BookingId}/qr")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client().GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Guid.NewGuid()).GetAsync($"/api/v1/checkouts/{a.OrderId}")).StatusCode);
    }
}
