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
using WaterbusSystem.Application.DTOs.Requests.Payments;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Payments;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services.Payment;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.WebApi.Middlewares;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CommercialHttpTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private TestDbContextFactory.TripSeedData _seed = null!;
    private readonly CommercialCheckoutTests.Gateway _gateway = new();
    private readonly string _jwtSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly string _callbackSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private const string CallbackTmnCode = "HTTPTEST01";
    private bool _verifyCallbackSignatures;

    public async Task InitializeAsync()
    {
        using var signingKey = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Database=NotUsed;Integrated Security=True",
            ["JwtSettings:Secret"] = _jwtSecret,
            ["VnPay:TmnCode"] = CallbackTmnCode,
            ["VnPay:HashSecret"] = _callbackSecret,
            ["Qr:PrivateKeyPem"] = signingKey.ExportPkcs8PrivateKeyPem()
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<ApplicationDbContext>();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        builder.Services.AddScoped(_ => new ApplicationDbContext(dbOptions, null));
        builder.Services.AddSingleton<IDistributedLockService, FakeDistributedLockService>();
        builder.Services.AddScoped<ICommercialTransactionRunner, DirectCommercialTransactionRunner>();
        builder.Services.AddSingleton<ICheckoutSessionStore, CommercialHandlerFactory.MemoryStore>();
        builder.Services.AddScoped<IVnPayService>(sp => _verifyCallbackSignatures
            ? ActivatorUtilities.CreateInstance<VnPayService>(sp)
            : _gateway);
        builder.Services.RemoveAll<IRefundGateway>();
        builder.Services.AddSingleton<IRefundGateway>(_gateway);
        builder.Services.AddScoped<ValidateRequestFilter>();
        builder.Services.AddControllers().AddApplicationPart(typeof(BookingsController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new OpenApiInfo { Title = "Commercial HTTP tests", Version = "v1" }));
        builder.Services.AddRateLimiter(o => o.AddPolicy("BookingPolicy", _ => RateLimitPartition.GetNoLimiter("http-tests")));
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseSwagger();
        _app.UseSwaggerUI();
        _app.UseRateLimiter();
        _app.UseMiddleware<GuestAccessMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        using var scope = _app.Services.CreateScope();
        _seed = TestDbContextFactory.SeedTrip(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    [Fact]
    public async Task SwaggerBookingValidationAndGuestReadContractsWorkOverHttp()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/swagger/index.html")).StatusCode);
        using var swagger = JsonDocument.Parse(await _client.GetStringAsync("/swagger/v1/swagger.json"));
        Assert.True(swagger.RootElement.GetProperty("paths").TryGetProperty("/api/v1/Payments/vnpay-ipn", out _));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/checkouts/qr-public-key")).StatusCode);
        var invalid = await _client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(Guid.Empty, [],
            Guid.Empty, Guid.Empty, "", "invalid", null));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        var created = await _client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(_seed.Trip.Id, [_seed.Seat1.Id],
            _seed.StationA.Id, _seed.StationC.Id, "Buyer", "buyer@example.com", null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = (await created.Content.ReadFromJsonAsync<BookingResponse>())!;
        Assert.Equal(15000m, booking.TotalAmount);
        Assert.NotNull(created.Headers.Location);
        Assert.Equal($"/api/v1/Checkouts/{booking.OrderId}", created.Headers.Location.AbsolutePath);
        Assert.Empty(created.Headers.Location.Query);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(created.Headers.Location)).StatusCode);
        _client.DefaultRequestHeaders.Add("X-Guest-Token", booking.AccessToken);
        var detail = await _client.GetFromJsonAsync<CheckoutDetailDto>(created.Headers.Location);
        Assert.Equal(booking.OrderId, detail!.OrderId);
        Assert.Equal(booking.BookingId, Assert.Single(detail!.Bookings).BookingId);
    }

    [Fact]
    public async Task HoldSubmitIpnQrAndAdminRefundFlowPreservesHttpContracts()
    {
        var held = await _client.PostAsJsonAsync("/api/v1/checkouts/holds", new CreateCheckoutHoldRequest([
            new(_seed.Trip.Id, [_seed.Seat1.Id], _seed.StationA.Id, _seed.StationC.Id)]));
        Assert.Equal(HttpStatusCode.Created, held.StatusCode);
        var checkout = (await held.Content.ReadFromJsonAsync<CheckoutResponse>())!;
        var bookingId = checkout.Bookings.Single().BookingId;
        _client.DefaultRequestHeaders.Add("X-Guest-Token", checkout.AccessToken);
        var submitted = await _client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.OrderId}/submit",
            new SubmitCheckoutRequest("Buyer", "buyer@example.com", null));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var initiated = await _client.PostAsJsonAsync("/api/v1/payments/create-vnpay-url", new CreateVnPayUrlRequest(checkout.OrderId));
        Assert.Equal(HttpStatusCode.OK, initiated.StatusCode);
        var attempt = (await initiated.Content.ReadFromJsonAsync<CreateVnPayUrlResponseDto>())!;
        var ipnUrl = $"/api/v1/payments/vnpay-ipn?vnp_TmnCode=test&vnp_TxnRef={attempt.MerchantReference}" +
            $"&vnp_TransactionNo=http-transaction&vnp_Amount={attempt.Amount * 100:0}&vnp_ResponseCode=00&vnp_TransactionStatus=00";
        var ipn = await _client.GetFromJsonAsync<VnPayIpnResultDto>(ipnUrl);
        Assert.Equal("00", ipn!.RspCode);
        var replay = await _client.PostAsync(ipnUrl, null);
        Assert.Equal("02", (await replay.Content.ReadFromJsonAsync<VnPayIpnResultDto>())!.RspCode);
        var qr = await _client.GetFromJsonAsync<BookingQrCredential>($"/api/v1/checkouts/{checkout.OrderId}/bookings/{bookingId}/qr");
        Assert.Equal("RS256", qr!.Algorithm);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/v1/checkouts/{checkout.OrderId}")).StatusCode);
        var request = new RequestRefundRequest(bookingId, "cancelled");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/refunds/request", request)).StatusCode);
        var adminId = Guid.NewGuid();
        var token = new JwtSecurityToken("WaterbusSystemApi", "WaterbusClients", [
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Role, "Admin")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await context.Trips.SingleAsync()).Status = TripStatus.Cancelled;
            await context.SaveChangesAsync();
        }
        _gateway.Outcome = RefundGatewayResult.Failed("manual required");
        var requested = await _client.PostAsJsonAsync("/api/v1/refunds/request", request);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var refund = (await requested.Content.ReadFromJsonAsync<RequestRefundResultDto>())!;
        Assert.True(refund.RequiresManualAction);
        var invalid = await _client.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(refund.RefundTransactionId, "", null, refund.Amount));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var confirmed = await _client.PostAsJsonAsync("/api/v1/refunds/confirm-manual",
            new ConfirmManualRefundRequest(refund.RefundTransactionId, "bank receipt", "manual-bank", refund.Amount));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("Succeeded", (await confirmed.Content.ReadFromJsonAsync<ConfirmManualRefundResultDto>())!.Status);
    }

    private async Task<Dictionary<string, string>> SignedPendingPaymentCallbackAsync()
    {
        _verifyCallbackSignatures = true;
        var created = await _client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(_seed.Trip.Id, [_seed.Seat1.Id],
            _seed.StationA.Id, _seed.StationC.Id, "Buyer", "buyer@example.com", null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = (await created.Content.ReadFromJsonAsync<BookingResponse>())!;
        _client.DefaultRequestHeaders.Add("X-Guest-Token", booking.AccessToken);
        var initiated = await _client.PostAsJsonAsync("/api/v1/payments/create-vnpay-url", new CreateVnPayUrlRequest(booking.OrderId));
        Assert.Equal(HttpStatusCode.OK, initiated.StatusCode);
        var attempt = (await initiated.Content.ReadFromJsonAsync<CreateVnPayUrlResponseDto>())!;
        var fields = new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = CallbackTmnCode, ["vnp_TxnRef"] = attempt.MerchantReference,
            ["vnp_TransactionNo"] = "signed-http-transaction", ["vnp_Amount"] = ((long)(attempt.Amount * 100)).ToString(),
            ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = "00", ["vnp_OrderInfo"] = "Waterbus payment + test"
        };
        var signedData = string.Join("&", fields.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => WebUtility.UrlEncode(p.Key) + "=" + WebUtility.UrlEncode(p.Value)));
        fields["vnp_SecureHash"] = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(_callbackSecret),
            Encoding.UTF8.GetBytes(signedData))).ToLowerInvariant();
        return fields;
    }

    private async Task<VnPayIpnResultDto> PostCallbackAsync(Dictionary<string, string> fields, HttpContent? body = null)
    {
        var query = string.Join("&", fields.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));
        var response = await _client.PostAsync("/api/v1/payments/vnpay-ipn?" + query, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VnPayIpnResultDto>())!;
    }

    private async Task AssertPaymentStateAsync(bool confirmed)
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(confirmed ? OrderStatus.Confirmed : OrderStatus.PendingPayment, (await context.PurchaseOrders.SingleAsync()).Status);
        Assert.Equal(confirmed ? FinancialTransactionStatus.Succeeded : FinancialTransactionStatus.Requested,
            (await context.FinancialTransactions.SingleAsync()).Status);
        Assert.Equal(confirmed ? 1 : 0, await context.Tickets.CountAsync());
    }

    [Fact]
    public async Task SignedQueryPostIpnConfirmsPayment()
    {
        var fields = await SignedPendingPaymentCallbackAsync();
        Assert.Equal("00", (await PostCallbackAsync(fields)).RspCode);
        await AssertPaymentStateAsync(true);
    }

    [Fact]
    public async Task InvalidSignaturePostIpnDoesNotChangePayment()
    {
        var fields = await SignedPendingPaymentCallbackAsync();
        fields["vnp_Amount"] = "1";
        Assert.Equal("97", (await PostCallbackAsync(fields)).RspCode);
        await AssertPaymentStateAsync(false);
    }

    [Fact]
    public async Task DuplicateSignedPostIpnIssuesTicketsOnce()
    {
        var fields = await SignedPendingPaymentCallbackAsync();
        Assert.Equal("00", (await PostCallbackAsync(fields)).RspCode);
        Assert.Equal("02", (await PostCallbackAsync(fields)).RspCode);
        await AssertPaymentStateAsync(true);
    }

    [Fact]
    public async Task FormOnlyPostIpnDoesNotBypassQueryOnlyContract()
    {
        var fields = await SignedPendingPaymentCallbackAsync();
        using var body = new FormUrlEncodedContent(fields);
        var response = await _client.PostAsync("/api/v1/payments/vnpay-ipn", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("97", (await response.Content.ReadFromJsonAsync<VnPayIpnResultDto>())!.RspCode);
        await AssertPaymentStateAsync(false);
    }

    [Fact]
    public async Task PostIpnUsesSignedQueryInsteadOfUnsignedFormOverrides()
    {
        var fields = await SignedPendingPaymentCallbackAsync();
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["vnp_Amount"] = "1", ["vnp_TxnRef"] = "another-payment", ["vnp_TransactionStatus"] = "02"
        });
        Assert.Equal("00", (await PostCallbackAsync(fields, body)).RspCode);
        await AssertPaymentStateAsync(true);
    }
}
