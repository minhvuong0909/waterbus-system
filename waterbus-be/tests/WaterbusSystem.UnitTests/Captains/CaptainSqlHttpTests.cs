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
using Microsoft.IdentityModel.Tokens;
using WaterbusSystem.Application;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Trips;
using WaterbusSystem.Application.Features.Boats.Services;
using WaterbusSystem.Application.Features.SeatInventory.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.WebApi.Middlewares;
using WaterbusSystem.WebApi.Realtime;
using Xunit;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.SignalR.Client;

namespace WaterbusSystem.UnitTests.Captains;

/// <summary>
/// BR-CAP: tài khoản, phân quyền, thu hồi phiên, phân công và điểm danh trên SQL/HTTP/SignalR thật.
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class CaptainSqlHttpTests : IAsyncLifetime
{
    private sealed class Clock : IDateTimeService { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; }
    private readonly Clock _clock = new();
    private string? _connection;
    private WebApplication _app = null!;
    private readonly string _jwtSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private TestDbContextFactory.TripSeedData _seed = null!;
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured)
        { InitialCatalog = "WaterbusDomainTests_Captain_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using (var c = Context()) await c.Database.MigrateAsync();

        using var key = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connection,
            ["JwtSettings:Secret"] = _jwtSecret,
            ["VnPay:TmnCode"] = "POLTEST01",
            ["VnPay:HashSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Qr:PrivateKeyPem"] = key.ExportPkcs8PrivateKeyPem()
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddSingleton<IDistributedLockService, FakeDistributedLockService>();
        builder.Services.AddSingleton<ICheckoutSessionStore, CommercialHandlerFactory.MemoryStore>();
        builder.Services.AddScoped<ValidateRequestFilter>();
        builder.Services.AddRealtime();
        builder.Services.AddSingleton<IDateTimeService>(_clock);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, WaterbusSystem.Infrastructure.Services.CurrentUserService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminTripsController).Assembly);
        builder.Services.AddRateLimiter(o => { o.AddPolicy("BookingPolicy", _ => RateLimitPartition.GetNoLimiter("t")); o.AddPolicy("AuthPolicy", _ => RateLimitPartition.GetNoLimiter("a")); });
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.MapControllers();
        _app.MapRealtimeHubs();
        await _app.StartAsync();

        await using var seed = Context();
        _seed = TestDbContextFactory.SeedTrip(seed);
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_Captain_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context();
        await c.Database.EnsureDeletedAsync();
    }

    private async Task<Guid> CreateCaptain(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/captains", new CreateCaptainRequest("Test Captain", "new-captain@example.com", "0901234567",
            "LICENSE-NEW", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), "Captain1234"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }
    private async Task<string> Login(HttpClient http)
    {
        var response = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "new-captain@example.com", password = "Captain1234" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }
    [SqlServerDomainFact]
    public async Task AdminCreatesListsEditsAndSuspendsCaptain_RevokesJwtAndLiveHub_WithoutChangingTripStatus()
    {
        using var admin = Http("Admin"); using var captain = Http();
        var id = await CreateCaptain(admin);
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/v1/captains?page=1&pageSize=10&search=new-captain");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/boats/{_seed.Boat.Id}/captain", new AssignCaptainRequest(id, "Captain absent; Admin replaces boat Captain"))).StatusCode);
        var token = await Login(captain);
        captain.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await captain.GetAsync("/api/v1/trips/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await captain.GetAsync("/api/v1/captains")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await captain.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/captain-attendance",
            new RecordCaptainAttendanceRequest(id, Guid.NewGuid(), "Self attendance forbidden"))).StatusCode);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = new HubConnectionBuilder().WithUrl(_app.Urls.Single() + "/hubs/boat-tracking", o => o.AccessTokenProvider = () => Task.FromResult<string?>(token)).Build();
        hub.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
        await hub.StartAsync(); await hub.InvokeAsync("JoinBoat", _seed.Boat.Id);
        await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() => hub.InvokeAsync("JoinBoat", _seed.OtherBoat.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}/status", new ChangeCaptainStatusRequest(CaptainStatus.Suspended, "License review"))).StatusCode);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.Unauthorized, (await captain.GetAsync("/api/v1/trips/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await captain.PostAsJsonAsync("/api/v1/auth/login", new { email = "new-captain@example.com", password = "Captain1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}/status", new ChangeCaptainStatusRequest(CaptainStatus.Active, "Review completed"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await captain.GetAsync("/api/v1/trips/operations")).StatusCode); // Old JWT stays revoked after reactivation.
        var fresh = await Login(captain);
        captain.DefaultRequestHeaders.Authorization = new("Bearer", fresh);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}", new UpdateCaptainRequest("Updated Captain", "new-captain@example.com", "0901234567", "LICENSE-NEW", DateOnly.MaxValue, "Renew license"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await captain.GetAsync("/api/v1/trips/operations")).StatusCode);
        await using var verify = Context();
        Assert.Equal(TripStatus.Scheduled, (await verify.Trips.SingleAsync()).Status);
        Assert.Equal(4, await verify.Set<CaptainAuditEvent>().CountAsync(a => a.CaptainId == id && a.Action != "AssignmentChanged"));
        Assert.True(await verify.Set<CaptainAlert>().AnyAsync(a => a.Type == "AssignmentChanged"));
    }
    [SqlServerDomainFact]
    public async Task AttendanceUsesFirstVisitMinusTwentyMinutes_ExactDeadlineOnTime_AlertIdempotent_LateAccepted_StartRequiresAttendance()
    {
        using var admin = Http("Admin");
        var now = _clock.Now;
        await using (var db = Context())
        {
            var calls = await db.TripStopCalls.OrderBy(c => c.VisitOrder).ToListAsync();
            calls[0].CheckInOpenAt = now.AddMinutes(20); calls[1].CheckInOpenAt = now.AddMinutes(100);
            await db.SaveChangesAsync();
        }
        var state = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trips/{_seed.Trip.Id}/captain-attendance");
        Assert.Equal(now, state.GetProperty("requiredAt").GetDateTimeOffset());
        using (var scope = _app.Services.CreateScope()) Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ICaptainOperationsService>().CheckAttendanceAsync());
        _clock.Now = now.AddTicks(1);
        using (var scope = _app.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ICaptainOperationsService>();
            Assert.Equal(1, await service.CheckAttendanceAsync()); Assert.Equal(0, await service.CheckAttendanceAsync());
        }
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/status", new { operation = "OpenBoarding" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/status", new { operation = "Start" })).StatusCode);
        var request = new RecordCaptainAttendanceRequest(_seed.Boat.CaptainUserId!.Value, _seed.Boat.CaptainAssignmentVersion, "Confirmed physical presence at station");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/captain-attendance", request with { Reason = "" })).StatusCode);
        var late = await admin.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/captain-attendance", request);
        Assert.Equal(HttpStatusCode.OK, late.StatusCode); Assert.True((await late.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isLate").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/captain-attendance", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/status", new { operation = "Start" })).StatusCode);
        await using var verify = Context(); Assert.Equal(1, await verify.Set<CaptainAttendance>().CountAsync()); Assert.Equal(1, await verify.Set<CaptainAlert>().CountAsync(a => a.Type == "CaptainMissing"));
    }
    [SqlServerDomainFact]
    public async Task AssignmentRejectsUnknownWrongRoleExpiredInactiveAndAnotherBoat_BlocksOperationalTrips_InvalidateOldAttendance()
    {
        using var admin = Http("Admin"); var id = await CreateCaptain(admin);
        var path = $"/api/v1/boats/{_seed.Boat.Id}/captain";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, new AssignCaptainRequest(Guid.NewGuid(), "Unknown"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, new AssignCaptainRequest(_seed.OtherBoat.CaptainUserId, "Already assigned"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}/status", new ChangeCaptainStatusRequest(CaptainStatus.Inactive, "Not ready"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, new AssignCaptainRequest(id, "Inactive"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}/status", new ChangeCaptainStatusRequest(CaptainStatus.Active, "Ready"))).StatusCode);
        await using (var db = Context()) { var u = await db.Users.SingleAsync(u => u.Id == id); u.CaptainLicenseExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, new AssignCaptainRequest(id, "Expired"))).StatusCode);
        await using (var db = Context()) { var u = await db.Users.SingleAsync(u => u.Id == id); u.CaptainLicenseExpiresOn = DateOnly.MaxValue; await db.SaveChangesAsync(); }
        var original = new RecordCaptainAttendanceRequest(_seed.Boat.CaptainUserId!.Value, _seed.Boat.CaptainAssignmentVersion, "Present");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/captain-attendance", original)).StatusCode);
        foreach (var status in new[] { TripStatus.Boarding, TripStatus.EnRoute, TripStatus.Suspended })
        {
            await using (var db = Context()) { (await db.Trips.SingleAsync()).Status = status; await db.SaveChangesAsync(); }
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, new AssignCaptainRequest(null, "Remove"))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, new AssignCaptainRequest(id, "Replace"))).StatusCode);
        }
        await using (var db = Context()) { (await db.Trips.SingleAsync()).Status = TripStatus.Scheduled; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(path, new AssignCaptainRequest(id, "Replace before boarding"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/trips/{_seed.Trip.Id}/captain-attendance", original)).StatusCode);
        var state = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trips/{_seed.Trip.Id}/captain-attendance"); Assert.Equal(JsonValueKind.Null, state.GetProperty("attendance").ValueKind);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(path, new AssignCaptainRequest(null, "Remove Captain"))).StatusCode);
        using var guest = Http();
        Assert.Empty((await guest.GetFromJsonAsync<JsonElement>("/api/v1/trips/search")).EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(_seed.Trip.Id, [_seed.Seat1.Id], _seed.StationA.Id, _seed.StationC.Id, "Guest", "guest@example.com", null))).StatusCode);
        await using var verify = Context(); Assert.Equal(0, await verify.PurchaseOrders.CountAsync()); Assert.Equal(1, await verify.Set<CaptainAttendance>().CountAsync());
    }

    private HttpClient Http(string? role = null)
    {
        var client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };
        if (role is not null)
        {
            var jwt = new JwtSecurityToken("WaterbusSystemApi", "WaterbusClients",
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
                expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }
        return client;
    }

}
