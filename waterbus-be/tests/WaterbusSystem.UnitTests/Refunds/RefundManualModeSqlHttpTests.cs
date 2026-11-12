using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WaterbusSystem.Application;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Features.Notifications.Templates;
using WaterbusSystem.Application.Features.Refunds;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Application.Validators.Refunds;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.Infrastructure.Services.Refunds;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.WebApi.Middlewares;
using Xunit;

namespace WaterbusSystem.UnitTests.Refunds;

/// <summary>
/// Refund Manual mode (quy trình hoàn thủ công có kiểm soát) trên SQL Server thật + HTTP thật: không bao giờ gọi gateway Refund của VNPay,
/// không refund trùng, phân bổ round-trip đúng, không có nhãn DEMO, refund VNPAY mơ hồ có sẵn không bị tự giải quyết.
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class RefundManualModeSqlHttpTests : IAsyncLifetime
{
    private string? _connection;
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly CommercialCheckoutTests.Gateway _vnpay = new();
    private readonly string _jwtSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = "WaterbusDomainTests_RefundManual_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using (var c = Context()) await c.Database.MigrateAsync();

        using var signingKey = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        // Cố ý KHÔNG đặt Refunds:Mode: kiểm chứng mặc định an toàn là Manual.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connection,
            ["JwtSettings:Secret"] = _jwtSecret,
            ["VnPay:TmnCode"] = "REFDEMO01",
            ["VnPay:HashSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Qr:PrivateKeyPem"] = signingKey.ExportPkcs8PrivateKeyPem()
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
        builder.Services.RemoveAll<ApplicationDbContext>();
        builder.Services.AddScoped(_ => new ApplicationDbContext(dbOptions, null));
        builder.Services.AddSingleton<IDistributedLockService, FakeDistributedLockService>();
        builder.Services.AddSingleton<ICheckoutSessionStore, CommercialHandlerFactory.MemoryStore>();
        builder.Services.AddScoped<IVnPayService>(_ => _vnpay);
        // Giống production: Manual + một gateway VNPAY tự động. Gateway VNPAY ở đây là bộ đếm: mọi lần gọi đều lộ ra.
        builder.Services.RemoveAll<IRefundGateway>();
        builder.Services.AddScoped<IRefundGateway, ManualRefundGateway>();
        builder.Services.AddSingleton<IRefundGateway>(_vnpay);
        builder.Services.AddScoped<IRefundGateway, VnPayRefundGateway>();   // adapter thật → IVnPayService.RefundAsync (được đếm)
        builder.Services.AddScoped<ValidateRequestFilter>();
        builder.Services.AddControllers().AddApplicationPart(typeof(RefundsController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new OpenApiInfo { Title = "Refund demo tests", Version = "v1" }));
        builder.Services.AddRateLimiter(o => o.AddPolicy("BookingPolicy", _ => RateLimitPartition.GetNoLimiter("refund-demo")));
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseRateLimiter();
        _app.UseMiddleware<GuestAccessMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()), Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_RefundManual_")) throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context(); await c.Database.EnsureDeletedAsync();
    }

    /// <summary>Cả hai đường tới VNPay Refund đều phải bằng 0: IRefundGateway của test và IVnPayService.RefundAsync (API thật).</summary>
    private void AssertNoVnPayRefund()
    {
        Assert.Equal(0, _vnpay.RefundCalls);
        Assert.Equal(0, _vnpay.VnPayRefundApiCalls);
    }

    private HttpClient As(string? role, Guid? userId = null)
    {
        var client = new HttpClient { BaseAddress = _client.BaseAddress, Timeout = TimeSpan.FromSeconds(60) };
        if (role is null) return client;
        var token = new JwtSecurityToken("WaterbusSystemApi", "WaterbusClients", [
            new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private sealed record Seeded(Guid OrderId, Guid OutboundBookingId, Guid ReturnBookingId, Guid OutboundTripId, Guid ReturnTripId, decimal PaymentAmount);

    /// <summary>Order round-trip đã thanh toán (2 booking × 15.000 = 30.000), vé đã phát hành. Chưa huỷ chuyến nào.</summary>
    private async Task<Seeded> PaidRoundTripAsync()
    {
        using var scope = _app.Services.CreateScope();
        var c = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seed = TestDbContextFactory.SeedTrip(c);
        var returnTrip = new Trip { Route = seed.Route, RouteId = seed.Route.Id, BoatId = seed.Boat.Id,
            DepartureTime = DateTimeOffset.UtcNow.AddDays(2), ArrivalTime = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30) };
        c.Trips.Add(returnTrip);
        foreach (var call in seed.Trip.TripStopCalls)
            c.TripStopCalls.Add(new TripStopCall { Trip = returnTrip, TripId = returnTrip.Id, RouteStop = call.RouteStop, VisitOrder = call.VisitOrder });
        await c.SaveChangesAsync();
        var checkout = await CommercialHandlerFactory.Checkout(c).CreateAsync(new CreateCheckoutRequest("Buyer", "buyer@example.com", null,
            [new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id),
             new(returnTrip.Id, [seed.Seat2.Id], seed.StationA.Id, seed.StationC.Id)]), default);
        var gateway = new CommercialCheckoutTests.Gateway();
        await CommercialHandlerFactory.Payment(c, gateway).CreateVnPayUrlAsync(checkout.OrderId, "127.0.0.1", checkout.AccessToken!, default);
        var attempt = await c.FinancialTransactions.SingleAsync(t => t.TransactionType == FinancialTransactionType.Payment);
        await CommercialHandlerFactory.Ipn(c, gateway).ConfirmIpnAsync(CommercialCheckoutTests.Ipn(attempt), default);
        var bookings = await c.Bookings.Where(b => b.OrderId == checkout.OrderId).OrderBy(b => b.TripId == seed.Trip.Id ? 0 : 1).ToListAsync();
        return new(checkout.OrderId, bookings[0].Id, bookings[1].Id, seed.Trip.Id, returnTrip.Id, attempt.Amount);
    }

    private async Task CancelTripAsync(Guid tripId)
    {
        await using var c = Context();
        (await c.Trips.SingleAsync(t => t.Id == tripId)).Status = TripStatus.Cancelled;
        await c.SaveChangesAsync();
    }

    private static async Task<RequestRefundResultDto> OkRefund(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<RequestRefundResultDto>())!;
    }

    [Fact]
    public void RefundModeDefaultsToManualAndBindsFromConfiguration()
    {
        Assert.Equal(RefundMode.Manual, new RefundOptions().Mode);
        var empty = new ConfigurationBuilder().Build().GetSection(RefundOptions.SectionName).Get<RefundOptions>() ?? new RefundOptions();
        Assert.Equal(RefundMode.Manual, empty.Mode);
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Refunds:Mode"] = "Gateway" }).Build()
            .GetSection(RefundOptions.SectionName).Get<RefundOptions>()!;
        Assert.Equal(RefundMode.Gateway, gateway.Mode);
    }

    [SqlServerDomainFact]
    public async Task ManualModeWithNoConfiguration_RequestAndConfirmOverHttp_NeverCallsVnPayRefund_AndIsNotMarkedDemo()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        using var admin = As("Admin");

        var requested = await OkRefund(await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.OutboundBookingId, "cancelled")));
        Assert.Equal("Manual", requested.Gateway);
        Assert.Equal("Processing", requested.Status);
        Assert.False(requested.IsDemo);
        Assert.True(requested.RequiresManualAction);
        Assert.Null(requested.Notice);
        Assert.Equal(15000m, requested.Amount);                 // chỉ phần phân bổ của booking chuyến đi
        Assert.True(requested.Amount < seeded.PaymentAmount);
        Assert.Single(requested.Tickets);
        AssertNoVnPayRefund();

        // validation: thiếu bằng chứng → 400; sai số tiền → 409; vẫn chưa hoàn thành
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(requested.RefundTransactionId, "", null, requested.Amount))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(requested.RefundTransactionId, "demo evidence", null, requested.Amount + 1))).StatusCode);

        var confirmedResponse = await admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(requested.RefundTransactionId, "demo evidence", "BANK-REF-1", requested.Amount));
        Assert.Equal(HttpStatusCode.OK, confirmedResponse.StatusCode);
        var confirmed = (await confirmedResponse.Content.ReadFromJsonAsync<ConfirmManualRefundResultDto>())!;
        Assert.Equal("Succeeded", confirmed.Status);
        Assert.False(confirmed.IsDemo);
        Assert.Null(confirmed.Notice);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(requested.RefundTransactionId, "demo evidence", null, requested.Amount))).StatusCode);

        await using var c = Context();
        var refund = await c.FinancialTransactions.SingleAsync(t => t.TransactionType == FinancialTransactionType.Refund);
        Assert.False(refund.IsDemoRefund);
        Assert.Equal("Manual", refund.GatewayName);
        Assert.NotNull(refund.ManualProcessedByAdminId);
        Assert.Equal("demo evidence", refund.ManualRefundEvidence);
        AssertNoVnPayRefund();

        // thông báo: đúng một email hoàn tiền thật, không nhãn DEMO, nêu thời gian 2–3 ngày làm việc
        var outbox = await c.EmailOutbox.SingleAsync(m => m.NotificationType == EmailNotificationType.RefundSucceeded);
        Assert.Equal(refund.Id, outbox.FinancialTransactionId);
        var data = (await new NotificationDataRepository(c).GetRefundSuccessAsync(refund.Id, default))!;
        Assert.False(data.IsDemo);
        var rendered = NotificationTemplates.RefundSucceeded(data, new EmailContext("Waterbus", null, null, null));
        Assert.StartsWith("Hoàn tiền thành công", rendered.Subject);
        Assert.DoesNotContain("DEMO", rendered.Subject + rendered.Text);
        Assert.DoesNotContain("KHÔNG có tiền thật", rendered.Text);
        Assert.Contains("2–3 ngày làm việc", rendered.Text);
    }

    [SqlServerDomainFact]
    public async Task RepeatedRequestAfterConfirmation_ReturnsTheSameSucceededRefund_WithNoManualActionAndNoSideEffects()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        using var admin = As("Admin");
        var created = await OkRefund(await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.OutboundBookingId, "cancelled")));
        Assert.True(created.RequiresManualAction);                 // trước khi xác nhận: còn việc thủ công
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(created.RefundTransactionId, "demo evidence", "DEMO-1", created.Amount))).StatusCode);

        async Task<(int Refunds, int Allocations, int Emails)> Counts()
        {
            await using var c = Context();
            return (await c.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund),
                await c.RefundTicketAllocations.CountAsync(),
                await c.EmailOutbox.CountAsync(m => m.NotificationType == EmailNotificationType.RefundSucceeded));
        }
        var before = await Counts();

        var again = await OkRefund(await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.OutboundBookingId, "again")));
        Assert.Equal(created.RefundTransactionId, again.RefundTransactionId);
        Assert.Equal("Succeeded", again.Status);
        Assert.False(again.IsDemo);
        Assert.False(again.RequiresManualAction);                  // hợp đồng đã sửa: không còn form xác nhận
        Assert.Null(again.Notice);
        Assert.Equal(before, await Counts());
        Assert.Equal((1, 1, 1), before);
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task RoundTripRefundsEachLegOnceWithItsOwnAllocation_AndNeverExceedsThePayment()
    {
        var seeded = await PaidRoundTripAsync();
        using var admin = As("Admin");
        // Chuyến về chưa huỷ: không đủ điều kiện, không tạo refund nào
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.ReturnBookingId, "x"))).StatusCode);
        await using (var c = Context()) Assert.Equal(0, await c.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund));

        await CancelTripAsync(seeded.OutboundTripId);
        var first = await OkRefund(await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.OutboundBookingId, "outbound")));
        var replay = await OkRefund(await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.OutboundBookingId, "again")));
        Assert.Equal(first.RefundTransactionId, replay.RefundTransactionId);

        await CancelTripAsync(seeded.ReturnTripId);
        var second = await OkRefund(await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.ReturnBookingId, "return")));
        Assert.NotEqual(first.RefundTransactionId, second.RefundTransactionId);
        Assert.Equal(15000m, first.Amount);
        Assert.Equal(15000m, second.Amount);
        Assert.Equal(seeded.PaymentAmount, first.Amount + second.Amount);

        await using var check = Context();
        var refunds = await check.FinancialTransactions.Include(r => r.RefundAllocations).Where(t => t.TransactionType == FinancialTransactionType.Refund).ToListAsync();
        Assert.Equal(2, refunds.Count);
        Assert.All(refunds, r => { Assert.False(r.IsDemoRefund); Assert.Equal("Manual", r.GatewayName); Assert.Single(r.RefundAllocations); });
        var allocatedTickets = refunds.SelectMany(r => r.RefundAllocations).Select(a => a.TicketId).ToList();
        Assert.Equal(allocatedTickets.Count, allocatedTickets.Distinct().Count());
        Assert.True(refunds.Sum(r => r.Amount) <= seeded.PaymentAmount);
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task ConcurrentRefundRequests_CreateExactlyOneRefund_AndZeroGatewayCalls()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        using var admin = As("Admin");
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() =>
            admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.OutboundBookingId, "race " + i)))));
        var results = new List<RequestRefundResultDto>();
        foreach (var r in responses) results.Add(await OkRefund(r));
        Assert.Single(results.Select(r => r.RefundTransactionId).Distinct());
        await using var c = Context();
        Assert.Equal(1, await c.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund));
        Assert.Equal(1, await c.RefundTicketAllocations.CountAsync());
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task ConcurrentAdminConfirmations_HaveExactlyOneWinner_AndOneNotification()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        using var admin = As("Admin");
        var refund = await OkRefund(await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.OutboundBookingId, "x")));
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() =>
            admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
                new ConfirmManualRefundRequest(refund.RefundTransactionId, "evidence " + i, "DEMO-" + i, refund.Amount)))));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var c = Context();
        Assert.Equal(1, await c.EmailOutbox.CountAsync(m => m.NotificationType == EmailNotificationType.RefundSucceeded));
        Assert.Equal(FinancialTransactionStatus.Succeeded, (await c.FinancialTransactions.SingleAsync(t => t.Id == refund.RefundTransactionId)).Status);
        AssertNoVnPayRefund();
    }

    [Fact]
    public void InvalidRefundModeIsRejected_NeverFallsThroughToRealRefunds()
    {
        var invalid = Microsoft.Extensions.Options.Options.Create(new RefundOptions { Mode = (RefundMode)0 });
        using var context = TestDbContextFactory.CreateDbContext();
        Assert.Throws<InvalidOperationException>(() => new RefundRequestService(new FinancialTransactionRepository(context),
            new BookingRepository(context), new EfUnitOfWork(context), new RequestRefundRequestValidator(), [_vnpay],
            new WaterbusSystem.UnitTests.TestHelpers.DirectCommercialTransactionRunner(), TestNotifications.Outbox(context), invalid));
        Assert.Equal(0, _vnpay.RefundCalls);
    }

    [SqlServerDomainFact]
    public async Task OnlyAdminCanRequestOrConfirm_RefundEndpointsRejectEveryOtherRole()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        var body = new RequestRefundRequest(seeded.OutboundBookingId, "x");
        using (var anonymous = As(null)) Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/refunds/request", body)).StatusCode);
        foreach (var role in new[] { "Passenger", "Staff", "Captain" })
        {
            using var other = As(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/refunds/request", body)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
                new ConfirmManualRefundRequest(Guid.NewGuid(), "e", null, 1m))).StatusCode);
        }
        await using var c = Context();
        Assert.Equal(0, await c.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund));
    }

    [SqlServerDomainFact]
    public async Task ExistingAmbiguousVnPayProcessingRefund_IsNeverAutoResolved_InManualMode()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        // Dữ liệu cũ: refund VNPAY timeout (Gateway mode trước đây) → Processing, không GatewayReference
        Guid ambiguousId;
        var timeout = new TimeoutGateway();
        using (var scope = _app.Services.CreateScope())
        {
            var c = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var legacy = new RefundRequestService(new FinancialTransactionRepository(c), new BookingRepository(c), new EfUnitOfWork(c),
                new RequestRefundRequestValidator(), [timeout], new SqlCommercialTransactionRunner(c), TestNotifications.Outbox(c), RefundTestOptions.Gateway);
            ambiguousId = (await legacy.RequestAsync(new(seeded.OutboundBookingId, "legacy"))).RefundTransactionId;
        }
        Assert.Equal(1, timeout.Calls);

        using var admin = As("Admin");
        var again = await OkRefund(await admin.PostAsJsonAsync("/api/v1/refunds/request", new RequestRefundRequest(seeded.OutboundBookingId, "demo")));
        Assert.Equal(ambiguousId, again.RefundTransactionId);
        Assert.Equal("VNPAY", again.Gateway);
        Assert.Equal("Processing", again.Status);
        Assert.False(again.IsDemo);                    // không bị đánh dấu lại thành demo
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(ambiguousId, "demo evidence", "DEMO", again.Amount))).StatusCode);

        await using var check = Context();
        var stored = await check.FinancialTransactions.SingleAsync(t => t.TransactionType == FinancialTransactionType.Refund);
        Assert.Equal(FinancialTransactionStatus.Processing, stored.Status);
        Assert.Equal("VNPAY", stored.GatewayName);
        Assert.Null(stored.GatewayReference);
        Assert.False(stored.IsDemoRefund);
        Assert.Equal(1, timeout.Calls);               // không gửi thêm
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task ManualModeFailsClosed_WhenNoManualGatewayIsRegistered_AndNeverTouchesTheAutomaticGateway()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        await using var c = Context();
        var service = new RefundRequestService(new FinancialTransactionRepository(c), new BookingRepository(c), new EfUnitOfWork(c),
            new RequestRefundRequestValidator(), [_vnpay], new SqlCommercialTransactionRunner(c), TestNotifications.Outbox(c), RefundTestOptions.Manual);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestAsync(new(seeded.OutboundBookingId, "x")));
        AssertNoVnPayRefund();
        await using var check = Context();
        Assert.Equal(0, await check.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund));
        Assert.DoesNotContain(await check.Tickets.Where(t => t.BookingId == seeded.OutboundBookingId).ToListAsync(),
            t => t.Status is TicketStatus.Cancelled or TicketStatus.Terminated);
    }

    [SqlServerDomainFact]
    public async Task GatewayMode_StillUsesTheAutomaticGateway_AndIsNotMarkedDemo()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        await using var c = Context();
        var service = new RefundRequestService(new FinancialTransactionRepository(c), new BookingRepository(c), new EfUnitOfWork(c),
            new RequestRefundRequestValidator(), [new ManualRefundGateway(Microsoft.Extensions.Logging.Abstractions.NullLogger<ManualRefundGateway>.Instance), _vnpay],
            new SqlCommercialTransactionRunner(c), TestNotifications.Outbox(c), RefundTestOptions.Gateway);
        var result = await service.RequestAsync(new(seeded.OutboundBookingId, "gateway"));
        Assert.Equal(1, _vnpay.RefundCalls);
        Assert.False(result.IsDemo);
        Assert.Null(result.Notice);
        Assert.Equal("VNPAY", result.Gateway);
        await using var check = Context();
        Assert.False((await check.FinancialTransactions.SingleAsync(t => t.TransactionType == FinancialTransactionType.Refund)).IsDemoRefund);
    }

    private sealed record CandidatesPage(List<RefundCandidateResponse> Items, int TotalCount);

    private static async Task<CandidatesPage> Candidates(HttpClient admin, Guid tripId)
    {
        var response = await admin.GetAsync($"/api/v1/refunds/candidates?tripId={tripId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CandidatesPage>())!;
    }

    private static async Task<BatchRefundRequestResultResponse> Batch(HttpClient admin, Guid tripId)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/refunds/batch-request", new BatchRequestRefundsRequest(tripId, "Bão"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BatchRefundRequestResultResponse>())!;
    }

    [SqlServerDomainFact]
    public async Task CandidatesAndBatchRequest_ListThenCreateOneManualRefund_AndASecondBatchCreatesNothing()
    {
        var seeded = await PaidRoundTripAsync();
        using var admin = As("Admin");
        // Chuyến chưa bị hủy/chấm dứt: chưa có căn cứ hoàn tiền (BR-RF-01) => 409 ở cả hai endpoint, không tạo gì.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.GetAsync($"/api/v1/refunds/candidates?tripId={seeded.OutboundTripId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/refunds/batch-request", new BatchRequestRefundsRequest(seeded.OutboundTripId))).StatusCode);

        await CancelTripAsync(seeded.OutboundTripId);
        var page = await Candidates(admin, seeded.OutboundTripId);
        var candidate = Assert.Single(page.Items);
        Assert.Equal(seeded.OutboundBookingId, candidate.BookingId);
        Assert.Equal(15000m, candidate.Amount);                           // chỉ phần phân bổ của chuyến đi (BR-RF-08)
        Assert.Equal("buyer@example.com", candidate.PurchaserEmail);
        Assert.Single(candidate.TicketCodes);

        var result = await Batch(admin, seeded.OutboundTripId);
        Assert.Equal(1, result.Requested);
        Assert.Equal(0, result.Skipped);
        Assert.Equal("Processing", result.Created.Single().Status);
        await using (var c = Context())
        {
            var refund = await c.FinancialTransactions.SingleAsync(t => t.TransactionType == FinancialTransactionType.Refund);
            Assert.Equal("Manual", refund.GatewayName);
            Assert.False(refund.IsDemoRefund);
            Assert.Equal(15000m, refund.Amount);
            Assert.Equal(seeded.OutboundBookingId, refund.RefundBookingId);
            Assert.All(await c.Tickets.Where(t => t.BookingId == seeded.OutboundBookingId).ToListAsync(), t => Assert.Equal(TicketStatus.Cancelled, t.Status));
        }

        // Đã nằm trong refund => không còn là candidate; gọi lại không hoàn trùng.
        Assert.Empty((await Candidates(admin, seeded.OutboundTripId)).Items);
        var again = await Batch(admin, seeded.OutboundTripId);
        Assert.Equal((0, 0), (again.Requested, again.Skipped));
        await using var check = Context();
        Assert.Equal(1, await check.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund));
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task BatchRequest_OnARoundTrip_RefundsOnlyTheCancelledLeg_AndLeavesTheOtherBookingUntouched()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        using var admin = As("Admin");

        var result = await Batch(admin, seeded.OutboundTripId);

        Assert.Equal(15000m, result.Created.Single().Amount);
        Assert.True(result.Created.Single().Amount < seeded.PaymentAmount);
        await using var c = Context();
        var returnBooking = await c.Bookings.Include(b => b.Tickets).SingleAsync(b => b.Id == seeded.ReturnBookingId);
        Assert.Equal(BookingStatus.Confirmed, returnBooking.Status);
        Assert.All(returnBooking.Tickets, t => Assert.Equal(TicketStatus.Valid, t.Status));
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task BatchRequest_SkipsANoShowTicket_SoItIsNeverRefunded()
    {
        var seeded = await PaidRoundTripAsync();
        await using (var c = Context())
        {
            foreach (var t in await c.Tickets.Where(t => t.BookingId == seeded.OutboundBookingId).ToListAsync()) t.BoardingStatus = BoardingStatus.NoShow;
            await c.SaveChangesAsync();
        }
        await CancelTripAsync(seeded.OutboundTripId);
        using var admin = As("Admin");

        Assert.Empty((await Candidates(admin, seeded.OutboundTripId)).Items);        // BR-RF-06
        var result = await Batch(admin, seeded.OutboundTripId);
        Assert.Equal((0, 0), (result.Requested, result.Skipped));
        await using var check = Context();
        Assert.Equal(0, await check.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund));
    }

    [SqlServerDomainFact]
    public async Task ConcurrentBatchRequests_CreateExactlyOneRefund()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        using var admin = As("Admin");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Batch(admin, seeded.OutboundTripId))));
        Assert.Single(results.SelectMany(r => r.Created).Select(x => x.RefundTransactionId).Distinct());
        await using var c = Context();
        Assert.Equal(1, await c.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund));
        Assert.Equal(1, await c.RefundTicketAllocations.CountAsync());
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task BatchEndpoints_RejectNonAdmins_InvalidInput_AndUnknownTrips()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        var list = $"/api/v1/refunds/candidates?tripId={seeded.OutboundTripId}";
        var body = new BatchRequestRefundsRequest(seeded.OutboundTripId);
        using (var anonymous = As(null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(list)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/refunds/batch-request", body)).StatusCode);
        }
        foreach (var role in new[] { "Passenger", "Staff", "Captain" })
        {
            using var other = As(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(list)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/refunds/batch-request", body)).StatusCode);
        }
        using var admin = As("Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/v1/refunds/candidates?tripId={Guid.Empty}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(list + "&pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/refunds/batch-request", new BatchRequestRefundsRequest(Guid.Empty))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/refunds/candidates?tripId={Guid.NewGuid()}")).StatusCode);
        await using var c = Context();
        Assert.Equal(0, await c.FinancialTransactions.CountAsync(t => t.TransactionType == FinancialTransactionType.Refund));
    }

    private static async Task<(HttpResponseMessage Response, string Text)> ExportCsv(HttpClient admin, Guid? tripId = null)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/refunds/export", new ExportRefundsRequest(tripId));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return (response, Encoding.UTF8.GetString(bytes));
    }

    [SqlServerDomainFact]
    public async Task ExportCsv_ListsPendingManualRefunds_MarksThem_AllowsReExport_AndDropsConfirmedOnes()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        var adminId = Guid.NewGuid();
        using var admin = As("Admin", adminId);
        var refundId = (await Batch(admin, seeded.OutboundTripId)).Created.Single().RefundTransactionId;

        var (first, text) = await ExportCsv(admin);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("text/csv", first.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("hoan-tien-", first.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        Assert.Equal("1", first.Headers.GetValues("X-Row-Count").Single());
        Assert.StartsWith("﻿Mã hoàn tiền", text);                 // BOM + tiêu đề
        Assert.Contains(refundId.ToString(), text);
        Assert.Contains("buyer@example.com", text);
        Assert.Contains(",15000,", text);
        Assert.Contains("Chuyến bị hủy", text);

        DateTimeOffset firstExport;
        await using (var c = Context())
        {
            var refund = await c.FinancialTransactions.SingleAsync(t => t.Id == refundId);
            Assert.NotNull(refund.ExportedAt);
            Assert.Equal(adminId, refund.ExportedByAdminId);
            Assert.Equal(FinancialTransactionStatus.Processing, refund.Status);       // xuất không đổi trạng thái tiền
            firstExport = refund.ExportedAt!.Value;
        }

        // Cho phép xuất lại: vẫn còn dòng, dấu "xuất lần cuối" được cập nhật.
        var (second, _) = await ExportCsv(admin);
        Assert.Equal("1", second.Headers.GetValues("X-Row-Count").Single());
        await using (var c = Context())
            Assert.True((await c.FinancialTransactions.SingleAsync(t => t.Id == refundId)).ExportedAt >= firstExport);

        // Sau khi Admin xác nhận đã hoàn: không còn trong danh sách và không bị ghi dấu thêm.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(refundId, "Mã GD hoàn 14055555", "14055555", 15000m))).StatusCode);
        DateTimeOffset beforeThird;
        await using (var c = Context()) beforeThird = (await c.FinancialTransactions.SingleAsync(t => t.Id == refundId)).ExportedAt!.Value;
        var (third, thirdText) = await ExportCsv(admin);
        Assert.Equal("0", third.Headers.GetValues("X-Row-Count").Single());
        Assert.DoesNotContain(refundId.ToString(), thirdText);
        await using (var c = Context())
            Assert.Equal(beforeThird, (await c.FinancialTransactions.SingleAsync(t => t.Id == refundId)).ExportedAt);
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task ExportCsv_CanBeLimitedToOneTrip_AndWithNothingPendingIsAnEmptyFileThatMarksNothing()
    {
        var seeded = await PaidRoundTripAsync();
        using var admin = As("Admin");

        var (empty, emptyText) = await ExportCsv(admin);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal("0", empty.Headers.GetValues("X-Row-Count").Single());
        Assert.Single(emptyText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));        // chỉ tiêu đề

        await CancelTripAsync(seeded.OutboundTripId);
        await CancelTripAsync(seeded.ReturnTripId);
        var outboundRefund = (await Batch(admin, seeded.OutboundTripId)).Created.Single().RefundTransactionId;
        var returnRefund = (await Batch(admin, seeded.ReturnTripId)).Created.Single().RefundTransactionId;

        var (one, oneText) = await ExportCsv(admin, seeded.OutboundTripId);
        Assert.Equal("1", one.Headers.GetValues("X-Row-Count").Single());
        Assert.Contains(outboundRefund.ToString(), oneText);
        Assert.DoesNotContain(returnRefund.ToString(), oneText);
        await using (var c = Context())
            Assert.Null((await c.FinancialTransactions.SingleAsync(t => t.Id == returnRefund)).ExportedAt);   // chuyến kia chưa được xuất

        var (all, _) = await ExportCsv(admin);
        Assert.Equal("2", all.Headers.GetValues("X-Row-Count").Single());
    }

    [SqlServerDomainFact]
    public async Task ExportCsv_RejectsNonAdmins_AndAnEmptyTripId()
    {
        await PaidRoundTripAsync();
        using (var anonymous = As(null))
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/refunds/export", new ExportRefundsRequest())).StatusCode);
        foreach (var role in new[] { "Passenger", "Staff", "Captain" })
        {
            using var other = As(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/refunds/export", new ExportRefundsRequest())).StatusCode);
        }
        using var admin = As("Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/refunds/export", new ExportRefundsRequest(Guid.Empty))).StatusCode);
    }

    private sealed record PendingPage(List<PendingManualRefund> Items, int TotalCount);

    [SqlServerDomainFact]
    public async Task PendingList_ShowsManualRefundsWaitingForStaff_WithoutMarkingThemExported()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        await CancelTripAsync(seeded.ReturnTripId);
        using var admin = As("Admin");
        var outbound = (await Batch(admin, seeded.OutboundTripId)).Created.Single().RefundTransactionId;
        await Batch(admin, seeded.ReturnTripId);

        var all = (await (await admin.GetAsync("/api/v1/refunds/pending")).Content.ReadFromJsonAsync<PendingPage>())!;
        Assert.Equal(2, all.TotalCount);
        Assert.All(all.Items, i => { Assert.Equal(15000m, i.Amount); Assert.Equal("buyer@example.com", i.PurchaserEmail); Assert.Null(i.LastExportedAt); });

        var paged = (await (await admin.GetAsync("/api/v1/refunds/pending?page=2&pageSize=1")).Content.ReadFromJsonAsync<PendingPage>())!;
        Assert.Equal(2, paged.TotalCount);
        Assert.Single(paged.Items);

        var one = (await (await admin.GetAsync($"/api/v1/refunds/pending?tripId={seeded.OutboundTripId}")).Content.ReadFromJsonAsync<PendingPage>())!;
        Assert.Equal(outbound, one.Items.Single().RefundId);

        await using (var c = Context())
            Assert.All(await c.FinancialTransactions.Where(t => t.TransactionType == FinancialTransactionType.Refund).ToListAsync(), t => Assert.Null(t.ExportedAt));

        // Đã xác nhận hoàn thì rời danh sách.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(outbound, "Mã GD hoàn 1", null, 15000m))).StatusCode);
        var after = (await (await admin.GetAsync("/api/v1/refunds/pending")).Content.ReadFromJsonAsync<PendingPage>())!;
        Assert.Equal(1, after.TotalCount);
        Assert.DoesNotContain(after.Items, i => i.RefundId == outbound);
    }

    [SqlServerDomainFact]
    public async Task PendingList_RejectsNonAdmins_AndBadPaging()
    {
        using (var anonymous = As(null)) Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/refunds/pending")).StatusCode);
        foreach (var role in new[] { "Passenger", "Staff", "Captain" })
        {
            using var other = As(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/v1/refunds/pending")).StatusCode);
        }
        using var admin = As("Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/refunds/pending?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/refunds/pending?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/v1/refunds/pending?tripId={Guid.Empty}")).StatusCode);
    }

    [SqlServerDomainFact]
    public async Task ExportCsv_NeverIncludesALegacyDemoRefund_SoStaffCannotPayOutSimulatedMoney()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        using var admin = As("Admin");
        var refundId = (await Batch(admin, seeded.OutboundTripId)).Created.Single().RefundTransactionId;
        await using (var c = Context())
        {
            (await c.FinancialTransactions.SingleAsync(t => t.Id == refundId)).IsDemoRefund = true;      // dữ liệu cũ từ thời còn chế độ Demo
            await c.SaveChangesAsync();
        }

        var (response, text) = await ExportCsv(admin);

        Assert.Equal("0", response.Headers.GetValues("X-Row-Count").Single());
        Assert.DoesNotContain(refundId.ToString(), text);
        await using var check = Context();
        Assert.Null((await check.FinancialTransactions.SingleAsync(t => t.Id == refundId)).ExportedAt);
    }

    private static Task<HttpResponseMessage> ConfirmBatch(HttpClient admin, params ConfirmManualRefundRequest[] items) =>
        admin.PostAsJsonAsync("/api/v1/refunds/confirm-manual/batch", new BatchConfirmManualRefundsRequest(items));

    [SqlServerDomainFact]
    public async Task ConfirmManualBatch_ConfirmsValidItems_ReportsTheRest_AndNeverConfirmsTwice()
    {
        var seeded = await PaidRoundTripAsync();
        await CancelTripAsync(seeded.OutboundTripId);
        await CancelTripAsync(seeded.ReturnTripId);
        using var admin = As("Admin");
        var good = (await Batch(admin, seeded.OutboundTripId)).Created.Single();
        var wrongAmount = (await Batch(admin, seeded.ReturnTripId)).Created.Single();
        var unknown = Guid.NewGuid();

        var response = await ConfirmBatch(admin,
            new ConfirmManualRefundRequest(good.RefundTransactionId, "Mã GD hoàn 1", "REF-1", good.Amount),
            new ConfirmManualRefundRequest(wrongAmount.RefundTransactionId, "Mã GD hoàn 2", "REF-2", wrongAmount.Amount + 1),
            new ConfirmManualRefundRequest(unknown, "Mã GD hoàn 3", null, 1000m));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<BatchConfirmManualRefundsResponse>())!;
        Assert.Equal((1, 2), (result.Confirmed, result.Failed));
        Assert.True(result.Items.Single(i => i.RefundTransactionId == good.RefundTransactionId).Succeeded);
        Assert.False(result.Items.Single(i => i.RefundTransactionId == wrongAmount.RefundTransactionId).Succeeded);
        Assert.False(result.Items.Single(i => i.RefundTransactionId == unknown).Succeeded);
        Assert.All(result.Items.Where(i => !i.Succeeded), i => Assert.False(string.IsNullOrWhiteSpace(i.Error)));

        await using (var c = Context())
        {
            Assert.Equal(FinancialTransactionStatus.Succeeded, (await c.FinancialTransactions.SingleAsync(t => t.Id == good.RefundTransactionId)).Status);
            Assert.Equal(FinancialTransactionStatus.Processing, (await c.FinancialTransactions.SingleAsync(t => t.Id == wrongAmount.RefundTransactionId)).Status);
            Assert.Equal(1, await c.EmailOutbox.CountAsync(m => m.NotificationType == EmailNotificationType.RefundSucceeded));
        }

        // Gửi lại khoản đã xác nhận: bị từ chối, không có email thứ hai.
        var again = (await (await ConfirmBatch(admin, new ConfirmManualRefundRequest(good.RefundTransactionId, "lần hai", null, good.Amount)))
            .Content.ReadFromJsonAsync<BatchConfirmManualRefundsResponse>())!;
        Assert.Equal((0, 1), (again.Confirmed, again.Failed));
        await using var check = Context();
        Assert.Equal(1, await check.EmailOutbox.CountAsync(m => m.NotificationType == EmailNotificationType.RefundSucceeded));
        AssertNoVnPayRefund();
    }

    [SqlServerDomainFact]
    public async Task ConfirmManualBatch_RejectsBadInput_AndNonAdmins()
    {
        using var admin = As("Admin");
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await ConfirmBatch(admin)).StatusCode);                                   // rỗng
        Assert.Equal(HttpStatusCode.BadRequest, (await ConfirmBatch(admin,
            new ConfirmManualRefundRequest(id, "e", null, 1m), new ConfirmManualRefundRequest(id, "e", null, 1m))).StatusCode);   // trùng
        Assert.Equal(HttpStatusCode.BadRequest, (await ConfirmBatch(admin, new ConfirmManualRefundRequest(id, "", null, 1m))).StatusCode);  // thiếu bằng chứng
        Assert.Equal(HttpStatusCode.BadRequest, (await ConfirmBatch(admin, Enumerable.Range(0, 101)
            .Select(_ => new ConfirmManualRefundRequest(Guid.NewGuid(), "e", null, 1m)).ToArray())).StatusCode);              // quá 100

        var body = new ConfirmManualRefundRequest(id, "e", null, 1m);
        using (var anonymous = As(null)) Assert.Equal(HttpStatusCode.Unauthorized, (await ConfirmBatch(anonymous, body)).StatusCode);
        foreach (var role in new[] { "Passenger", "Staff", "Captain" })
        {
            using var other = As(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await ConfirmBatch(other, body)).StatusCode);
        }
    }

    private sealed class TimeoutGateway : IRefundGateway
    {
        public string Name => "VNPAY";
        public bool SupportsAutomaticRefund => true;
        public int Calls { get; private set; }
        public Task<RefundGatewayResult> RefundAsync(RefundGatewayRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new TimeoutException("Refund response was lost.");
        }
    }
}
