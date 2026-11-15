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
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.UnitTests.Payments;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.DTOs.Responses.Bookings;

namespace WaterbusSystem.UnitTests.Captains;

/// <summary>
/// BR-CAP: tài khoản, phân quyền, thu hồi phiên, phân công và điểm danh trên SQL/HTTP/SignalR thật.
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class CaptainSqlHttpTests : IAsyncLifetime
{
    private sealed class Clock : IDateTimeService { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; }
    private readonly Clock _clock = new();
    private sealed class Mail : IVerificationEmailQueue
    {
        public string Link { get; private set; } = "";
        public bool TryEnqueue(string email, string name, string link) { Link = link; return true; }
    }
    private readonly Mail _mail = new();
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
            ["Email:Enabled"] = "true",
            ["Email:FromAddress"] = "test@waterbus.example.com",
            ["Email:Smtp:Host"] = "smtp.example.com",
            ["Email:PublicBaseUrl"] = "https://waterbus.example.com",
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
        builder.Services.AddSingleton<IVerificationEmailQueue>(_mail);
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

    private async Task<Guid> CreateCaptain(HttpClient admin, bool activate = true)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/captains", new CreateCaptainRequest("Test Captain", "new-captain@example.com", "0901234567",
            "LICENSE-NEW", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        if (activate)
        {
            var token = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(_mail.Link).Fragment.TrimStart('#'))["token"].ToString();
            using var guest = Http();
            var activated = await guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token, password = "Captain1234" });
            Assert.Equal(HttpStatusCode.NoContent, activated.StatusCode);
        }
        return id;
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
        Assert.Equal(6, await verify.Set<CaptainAuditEvent>().CountAsync(a => a.CaptainId == id && a.Action != "AssignmentChanged"));
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

    private string InvitationToken() => Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(_mail.Link).Fragment.TrimStart('#'))["token"].ToString();

    [SqlServerDomainFact]
    public async Task InvitationIsOneTime_ResendRevokesOldLink_WeakPasswordDoesNotActivate_ExpiryIsEnforced()
    {
        using var admin = Http("Admin"); using var guest = Http();
        var id = await CreateCaptain(admin, activate: false);
        var pending = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/captains/{id}");
        Assert.True(pending.GetProperty("invitationPending").GetBoolean());
        Assert.False(pending.GetProperty("emailConfirmed").GetBoolean());
        await using (var db = Context())
        {
            var user = await db.Users.SingleAsync(u => u.Id == id);
            Assert.Null(user.PasswordHash); Assert.False(user.EmailConfirmed); Assert.False(user.IsActive);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/auth/login", new { email = "new-captain@example.com", password = "Captain1234" })).StatusCode);
        var old = InvitationToken();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/v1/captains/{id}/invitation", new { reason = "Recipient requested a fresh link" })).StatusCode);
        var fresh = InvitationToken(); Assert.NotEqual(old, fresh);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token = old, password = "Captain1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token = fresh, password = "weak" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token = fresh, password = "Captain1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token = fresh, password = "Different1234" })).StatusCode);
        await Login(guest);
        await using var verify = Context(); var active = await verify.Users.SingleAsync(u => u.Id == id);
        Assert.True(active.EmailConfirmed); Assert.NotEqual("Captain1234", active.PasswordHash);
        Assert.True(await verify.Set<CaptainAuditEvent>().AnyAsync(a => a.CaptainId == id && a.ActedByCaptainId == id && a.ActedByAdminId == null));
    }

    [SqlServerDomainFact]
    public async Task InvitationExpiredCannotActivate_AndConcurrentAcceptHasOneWinner()
    {
        using var admin = Http("Admin"); using var guest = Http(); var id = await CreateCaptain(admin, false);
        var token = InvitationToken();
        _app.Services.GetRequiredService<IOptions<PersonnelInvitationOptions>>().Value.TokenLifetimeHours = 0.00001;
        await Task.Delay(100);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token, password = "Captain1234" })).StatusCode);
        _app.Services.GetRequiredService<IOptions<PersonnelInvitationOptions>>().Value.TokenLifetimeHours = 24;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/v1/captains/{id}/invitation", new { reason = "Expired invite" })).StatusCode);
        token = InvitationToken();
        var attempts = await Task.WhenAll(guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token, password = "Captain1234" }),
            guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token, password = "Different1234" }));
        Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.BadRequest);
    }

    [SqlServerDomainFact]
    public async Task SuspendedInvitationCannotActivate_AndAdminCannotBypassAcceptance()
    {
        using var admin = Http("Admin"); using var guest = Http();
        var id = await CreateCaptain(admin, false); var token = InvitationToken();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}/status", new ChangeCaptainStatusRequest(CaptainStatus.Suspended, "Review required"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token, password = "Captain1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}/status", new ChangeCaptainStatusRequest(CaptainStatus.Active, "Bypass invitation"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/captains/{id}/invitation", new { reason = "Still suspended" })).StatusCode);
    }

    [SqlServerDomainFact]
    public async Task EmailChangeRequiresFreshActivation_RevokesExistingJwt_AndSuspendedProfileCannotBypassIt()
    {
        using var admin = Http("Admin"); using var captain = Http(); using var guest = Http();
        var id = await CreateCaptain(admin); var jwt = await Login(captain);
        captain.DefaultRequestHeaders.Authorization = new("Bearer", jwt);
        var update = new UpdateCaptainRequest("Captain", "changed-captain@example.com", "0901234567", "LICENSE-NEW", DateOnly.MaxValue, "Verified email change request");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}/status", new ChangeCaptainStatusRequest(CaptainStatus.Suspended, "Review"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}/status", new ChangeCaptainStatusRequest(CaptainStatus.Inactive, "Review completed"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/captains/{id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await captain.GetAsync("/api/v1/trips/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/auth/login", new { email = update.Email, password = "Captain1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await guest.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = id, token = InvitationToken(), password = "Changed1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/api/v1/auth/login", new { email = update.Email, password = "Changed1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await captain.GetAsync("/api/v1/trips/operations")).StatusCode);
    }

    [SqlServerDomainFact]
    public async Task LegacyBoatEndpointsRequireActualReason_RespectOneToOneAndOperationalGuard_AndAuditBothCaptains()
    {
        using var admin = Http("Admin"); var id = await CreateCaptain(admin);
        var original = _seed.Boat.CaptainUserId;
        var update = new UpdateBoatRequest("Edited boat", _seed.Boat.RegistrationNumber, _seed.Boat.TotalSeats, "Test", 2024, true, id);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/boats/{_seed.Boat.Id}", update)).StatusCode);
        await using (var db = Context()) { Assert.Equal(original, (await db.Boats.SingleAsync(b => b.Id == _seed.Boat.Id)).CaptainUserId); }
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/boats/{_seed.Boat.Id}", update with { CaptainChangeReason = "Original Captain unavailable" })).StatusCode);
        await using (var db = Context())
        {
            var audit = await db.Set<CaptainAuditEvent>().Where(a => a.BoatId == _seed.Boat.Id && a.Action == "AssignmentChanged").ToListAsync();
            Assert.Equal(2, audit.Count); Assert.All(audit, a => Assert.Equal("Original Captain unavailable", a.Reason));
            (await db.Trips.SingleAsync()).Status = TripStatus.Boarding; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/boats/{_seed.Boat.Id}", update with { CaptainUserId = null, CaptainChangeReason = "Remove" })).StatusCode);
        var create = new CreateBoatRequest("NEW-CAP", "New boat", "NEW-REG", 10, "Test", 2024, id, "Already assigned");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/boats", create)).StatusCode);
    }

    private sealed class PausedCommercial(ICommercialTransactionRunner inner) : ICommercialTransactionRunner
    {
        public readonly TaskCompletionSource Locked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct = default) => inner.RunAsync(async () =>
        { Locked.TrySetResult(); await Release.Task.WaitAsync(ct); return await action(); }, ct);
    }

    [SqlServerDomainFact]
    public async Task AttendanceJobRunsDuringRealBookingCheckoutAndPaymentTransactions_WithoutCommercialLockOrStatusChanges()
    {
        BookingResponse? held = null;
        foreach (var operation in new[] { "Booking", "Checkout", "Payment" })
        {
            // Mỗi thao tác giữ khóa thương mại thật và được tạm dừng bên trong transaction.
            await using var db = Context();
            var call = await db.TripStopCalls.SingleAsync(c => c.TripId == _seed.Trip.Id && c.VisitOrder == 1);
            call.CheckInOpenAt = _clock.Now.AddMinutes(19); await db.SaveChangesAsync();
            await using var commercial = Context(); var paused = new PausedCommercial(new SqlCommercialTransactionRunner(commercial));
            var seat = operation == "Booking" ? _seed.Seat1.Id : _seed.Seat2.Id;
            Task work; Task<BookingResponse>? bookingTask = null;
            using var gateway = new VerifiedVnPayFixture();
            if (operation == "Booking") work = bookingTask = CommercialHandlerFactory.Booking(commercial, transactions: paused)
                .CreateAsync(CommercialCheckoutTests.Command(_seed));
            else if (operation == "Checkout") work = CommercialHandlerFactory.Checkout(commercial, transactions: paused)
                .HoldAsync(new([new(_seed.Trip.Id, [seat], _seed.StationA.Id, _seed.StationC.Id)]));
            else
            {
                work = CommercialHandlerFactory.Payment(commercial, gateway.Gateway, paused)
                    .CreateVnPayUrlAsync(held!.OrderId, "127.0.0.1", held.AccessToken);

            }
            await paused.Locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                using var scope = _app.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ICaptainOperationsService>().CheckAttendanceAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(work.IsCompleted);
            }
            finally { paused.Release.TrySetResult(); }
            await work.WaitAsync(TimeSpan.FromSeconds(10));
            if (bookingTask is not null) held = await bookingTask;
            if (operation == "Payment")
            {
                var payment = await commercial.FinancialTransactions.SingleAsync();
                var response = await CommercialHandlerFactory.Confirmation(commercial, gateway.Gateway, new SqlCommercialTransactionRunner(commercial))
                    .ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount), default);
                Assert.Equal("00", response.RspCode);
            }
        }
        await using var verify = Context();
        Assert.Equal(TripStatus.Scheduled, (await verify.Trips.SingleAsync()).Status);
        Assert.Equal(1, await verify.Set<CaptainAlert>().CountAsync(a => a.Type == "CaptainMissing"));
        Assert.Single(await verify.Tickets.ToListAsync());
    }

    [SqlServerDomainFact]
    public async Task ConcurrentJobsAreIdempotent_Batched_ExcludeHistoryAndFuture_AndRespectFirstServedStop()
    {
        await using (var db = Context())
        {
            var calls = await db.TripStopCalls.OrderBy(c => c.VisitOrder).ToListAsync();
            calls[0].Status = "Skipped"; calls[0].CheckInOpenAt = _clock.Now.AddDays(-10);
            calls[1].CheckInOpenAt = _clock.Now.AddMinutes(21); // First served stop is not due yet.
            calls[2].CheckInOpenAt = _clock.Now.AddMinutes(1);
            await db.SaveChangesAsync();
        }
        using (var scope = _app.Services.CreateScope()) Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ICaptainOperationsService>().CheckAttendanceAsync());
        await using (var db = Context())
        {
            (await db.TripStopCalls.SingleAsync(c => c.VisitOrder == 2)).CheckInOpenAt = _clock.Now.AddMinutes(19);
            for (var i = 0; i < 20; i++)
            {
                var historical = new Trip { RouteId = _seed.Route.Id, BoatId = _seed.Boat.Id, Status = TripStatus.Completed, DepartureTime = _clock.Now.AddDays(-40), ArrivalTime = _clock.Now.AddDays(-39) };
                var future = new Trip { RouteId = _seed.Route.Id, BoatId = _seed.Boat.Id, DepartureTime = _clock.Now.AddYears(1), ArrivalTime = _clock.Now.AddYears(1).AddHours(1) };
                historical.TripStopCalls.Add(new TripStopCall { RouteStopId = _seed.Trip.TripStopCalls.First().RouteStopId, VisitOrder = 1, CheckInOpenAt = historical.DepartureTime.AddMinutes(-30) });
                future.TripStopCalls.Add(new TripStopCall { RouteStopId = _seed.Trip.TripStopCalls.First().RouteStopId, VisitOrder = 1, CheckInOpenAt = future.DepartureTime.AddMinutes(-30) });
                db.Trips.AddRange(historical, future);
            }
            await db.SaveChangesAsync();
        }
        var counts = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        { using var scope = _app.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<ICaptainOperationsService>().CheckAttendanceAsync(); }));
        Assert.Equal(1, counts.Sum());
        await using var verify = Context(); Assert.Single(await verify.Set<CaptainAlert>().Where(a => a.Type == "CaptainMissing").ToListAsync());
    }

    [SqlServerDomainFact]
    public async Task AttendanceJobLimitsEachBatch_AndDrainsWithoutDuplicateAlerts()
    {
        _app.Services.GetRequiredService<IOptions<WaterbusSystem.Application.Features.Captains.CaptainOptions>>().Value.AlertBatchSize = 2;
        await using (var db = Context())
        {
            (await db.TripStopCalls.SingleAsync(c => c.VisitOrder == 1)).CheckInOpenAt = _clock.Now.AddMinutes(19);
            for (var i = 0; i < 4; i++)
            {
                var trip = new Trip { RouteId = _seed.Route.Id, BoatId = _seed.Boat.Id, DepartureTime = _clock.Now.AddHours(1), ArrivalTime = _clock.Now.AddHours(2) };
                // A delayed, still Scheduled trip remains due even after its planned arrival.
                if (i == 0) { trip.DepartureTime = _clock.Now.AddDays(-2); trip.ArrivalTime = _clock.Now.AddDays(-1); }
                trip.TripStopCalls.Add(new TripStopCall { RouteStopId = _seed.Trip.TripStopCalls.First().RouteStopId, VisitOrder = 1, CheckInOpenAt = _clock.Now.AddMinutes(19) });
                db.Trips.Add(trip);
            }
            await db.SaveChangesAsync();
        }
        foreach (var expected in new[] { 2, 2, 1, 0 })
        {
            using var scope = _app.Services.CreateScope();
            Assert.Equal(expected, await scope.ServiceProvider.GetRequiredService<ICaptainOperationsService>().CheckAttendanceAsync());
        }
        await using var verify = Context();
        Assert.Equal(5, await verify.Set<CaptainAlert>().CountAsync(a => a.Type == "CaptainMissing"));
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
