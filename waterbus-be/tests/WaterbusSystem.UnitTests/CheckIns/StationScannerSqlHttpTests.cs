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
using Moq;
using WaterbusSystem.Application;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.WebApi.Middlewares;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

[Collection(SqlCollections.Serial)]
public sealed class StationScannerSqlHttpTests : IAsyncLifetime
{
    private const string Devices = "/api/v1/admin/scanner-devices";
    private const string Shifts = "/api/v1/admin/staff-station-shifts";
    private const string CheckIns = "/api/v1/checkins";
    private const string Pair = "/api/v1/scanner/pair";
    private const string Status = "/api/v1/scanner/status";
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _staffA = Guid.NewGuid();
    private readonly Guid _staffB = Guid.NewGuid();
    private readonly string _jwtSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private string? _connection;
    private WebApplication? _app;
    private DateTimeOffset _now;
    private Guid _stationA, _stationB, _callA, _callB, _otherTripAtA, _ticket, _onlineTicket;

    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER") is not { Length: > 0 } configured) return;
        _connection = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = "WaterbusDomainTests_StationScanner_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        await using var db = Context();
        await db.Database.MigrateAsync();
        var scenario = CheckInScenarioFactory.SeedCheckInScenario(db, _staffA, withAssignment: false);
        _now = scenario.Now;
        _stationA = scenario.Seed.StationA.Id;
        _stationB = scenario.Seed.StationB.Id;
        _callA = scenario.BoardingCall.Id;
        _callB = scenario.Seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 2).Id;
        _ticket = scenario.Ticket.Id;
        var other = new Trip
        {
            ScheduleId = scenario.Seed.Trip.ScheduleId, RouteId = scenario.Seed.Route.Id, BoatId = scenario.Seed.Boat.Id,
            DepartureTime = scenario.Seed.Trip.DepartureTime.AddHours(1),
            ArrivalTime = scenario.Seed.Trip.DepartureTime.AddHours(2),
            SalesCloseAt = scenario.Seed.Trip.DepartureTime.AddMinutes(55),
            Status = TripStatus.Scheduled, TripType = TripType.Commuter,
        };
        var otherCall = new TripStopCall
        {
            RouteStopId = scenario.BoardingCall.RouteStopId, VisitOrder = 1,
            CheckInOpenAt = _now.AddMinutes(-1), CheckInCloseAt = _now.AddMinutes(10)
        };
        other.TripStopCalls.Add(otherCall);
        _otherTripAtA = otherCall.Id;
        db.Trips.Add(other);

        var reservation = new SeatReservation
        {
            TripId = scenario.Seed.Trip.Id, SeatId = scenario.Seed.Seat2.Id, BookingId = scenario.Booking.Id,
            Booking = scenario.Booking, BoardingStopOrder = 1, DisembarkingStopOrder = 3,
            Status = ReservationStatus.Confirmed, PassengerName = "Second passenger",
            SeatCodeAtSale = scenario.Seed.Seat2.SeatCode, SeatClassAtSale = scenario.Seed.StandardClass.Name,
            QuotedFare = TestDbContextFactory.StandardCommuterFare, PaidAllocation = TestDbContextFactory.StandardCommuterFare,
        };
        var payment = await db.FinancialTransactions.SingleAsync();
        var secondTicket = Ticket.Issue(scenario.Booking, reservation, payment, TripType.Commuter, _now);
        db.SeatReservations.Add(reservation);
        db.Tickets.Add(secondTicket);
        _onlineTicket = secondTicket.Id;

        var staffRole = new ApplicationRole { Id = Guid.NewGuid(), Name = "Staff", NormalizedName = "STAFF" };
        db.Roles.Add(staffRole);
        foreach (var id in new[] { _staffA, _staffB })
        {
            db.Users.Add(new ApplicationUser { Id = id, UserName = id.ToString(), IsActive = true, FullName = "Scanner test Staff" });
            db.UserRoles.Add(new() { UserId = id, RoleId = staffRole.Id });
        }
        await db.SaveChangesAsync();

        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(() => _now);
        using var key = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connection,
            ["JwtSettings:Secret"] = _jwtSecret,
            ["VnPay:TmnCode"] = "SCANTEST",
            ["VnPay:HashSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Qr:PrivateKeyPem"] = key.ExportPkcs8PrivateKeyPem()
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<IDateTimeService>();
        builder.Services.AddSingleton(clock.Object);
        builder.Services.AddScoped<ValidateRequestFilter>();
        builder.Services.AddControllers().AddApplicationPart(typeof(CheckInsController).Assembly);
        builder.Services.AddRateLimiter(o => o.AddPolicy("ScannerPairingPolicy", _ => RateLimitPartition.GetNoLimiter("station-scanner-test")));
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.MapControllers();
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_StationScanner_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
    }

    [SqlServerDomainFact]
    public async Task OnlineAndOffline_AreBoundToAuthenticatedDeviceShiftAndBoardingCall()
    {
        using var admin = Client(_admin, "Admin");
        using var staff = Client(_staffA);
        var primaryId = await Post<Guid>(admin, Devices, new CreateStationScannerRequest(_stationA, "Gate 1"));
        var secondaryId = await Post<Guid>(admin, Devices, new CreateStationScannerRequest(_stationA, "Gate 2"));
        var wrongStationId = await Post<Guid>(admin, Devices, new CreateStationScannerRequest(_stationB, "Gate 3"));

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync($"{Devices}/{primaryId}/invitation", null)).StatusCode);
        staff.DefaultRequestHeaders.Add("X-Scanner-Credential", "SC-ST01-001");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"{CheckIns}/assignments/mine")).StatusCode);
        staff.DefaultRequestHeaders.Remove("X-Scanner-Credential");

        var invite = await Invite(admin, primaryId);
        var races = await Task.WhenAll(staff.PostAsJsonAsync(Pair, new RedeemScannerInvitationRequest(invite.Activation)),
            staff.PostAsJsonAsync(Pair, new RedeemScannerInvitationRequest(invite.Activation)));
        var primary = (await Assert.Single(races, r => r.IsSuccessStatusCode).Content.ReadFromJsonAsync<ScannerPairingResponse>())!;
        Assert.Contains(races, r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(_stationA, primary.Scanner.StationId);
        using var first = Client(_staffA, credential: primary.Credential);
        var other = await PairDevice(admin, staff, secondaryId);
        using var second = Client(_staffA, credential: other.Credential);
        var wrong = await PairDevice(admin, staff, wrongStationId);
        Assert.NotEqual(primary.Scanner.DeviceCode, other.Scanner.DeviceCode);
        using var cross = Client(_staffA, credential: wrong.Credential);

        var firstShift = new CreateStaffShiftRequest(_staffA, _stationA, _now.AddMinutes(-10), _now.AddHours(1));
        await Post<Guid>(admin, Shifts, firstShift);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(Shifts, firstShift with { StationId = _stationB })).StatusCode);
        await Post<Guid>(admin, Shifts, firstShift with { StationId = _stationB, ActiveFrom = firstShift.ActiveUntil, ActiveUntil = _now.AddHours(2) });

        var draft = new CreatePrimaryScannerRequest(primaryId, _callA, _now, _now.AddMinutes(15));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{CheckIns}/assignments", draft with { DeviceId = wrongStationId })).StatusCode);
        var attempts = await Task.WhenAll(admin.PostAsJsonAsync($"{CheckIns}/assignments", draft),
            admin.PostAsJsonAsync($"{CheckIns}/assignments", draft with { DeviceId = secondaryId }));
        Assert.Single(attempts, r => r.IsSuccessStatusCode);
        Assert.Contains(attempts, r => r.StatusCode == HttpStatusCode.Conflict);

        var firstManifest = await first.GetFromJsonAsync<ScannerManifestResponse>($"{CheckIns}/manifest/{_callA}");
        var secondManifest = await second.GetFromJsonAsync<ScannerManifestResponse>($"{CheckIns}/manifest/{_callA}");
        var primaryHttp = firstManifest!.OfflineGrant is null ? second : first;
        var secondaryHttp = primaryHttp == first ? second : first;
        var primaryIdentity = primaryHttp == first ? primary.Scanner : other.Scanner;
        var secondaryIdentity = primaryHttp == first ? other.Scanner : primary.Scanner;
        var grant = Assert.IsType<ScannerOfflineGrantResponse>(firstManifest.OfflineGrant ?? secondManifest!.OfflineGrant);
        Assert.Null(primaryHttp == first ? secondManifest!.OfflineGrant : firstManifest.OfflineGrant);
        var mine = await first.GetFromJsonAsync<List<MyScannerAssignmentResponse>>($"{CheckIns}/assignments/mine") ?? [];
        Assert.NotEmpty(mine);
        // Staff phân biệt được các chuyến cùng tuyến/cùng bến qua giờ khởi hành thật của chuyến.
        Assert.All(mine, a => Assert.NotEqual(default, a.TripDepartureTime));
        Assert.Equal(HttpStatusCode.Forbidden, (await cross.GetAsync($"{CheckIns}/manifest/{_callA}")).StatusCode);

        Assert.Equal("NotAssigned", (await Online(cross, _ticket, _callA)).Outcome);
        Assert.Equal("NotAssigned", (await Online(first, _ticket, _callB)).Outcome);
        Assert.Equal("InvalidStop", (await Online(first, _ticket, _otherTripAtA)).Outcome);
        Assert.Equal("Success", (await Online(secondaryHttp, _onlineTicket, _callA)).Outcome);

        var offline = new CheckInEventInput(_ticket, _callA, Guid.NewGuid().ToString(), _now, null, grant.Id);
        Assert.Equal("NotAssigned", (await Sync(secondaryHttp, offline)).Outcome);
        Assert.Equal("Success", (await Sync(primaryHttp, offline)).Outcome);
        Assert.True((await Sync(primaryHttp, offline)).WasAlreadySynced);

        using var differentStaff = Client(_staffB, credential: primaryHttp == first ? primary.Credential : other.Credential);
        Assert.Equal(HttpStatusCode.OK, (await differentStaff.GetAsync(Status)).StatusCode);
        Assert.Equal("NotAssigned", (await Online(differentStaff, _ticket, _callA)).Outcome);

        await using var db = Context();
        var storedOffline = await db.CheckInEvents.SingleAsync(e => e.ClientEventId == offline.ClientEventId);
        Assert.Equal(primaryIdentity.DeviceId, storedOffline.ScannerDeviceId);
        Assert.Equal(primaryIdentity.EnrollmentId, storedOffline.ScannerEnrollmentId);
        Assert.Equal(_staffA, storedOffline.StaffAccountId);
        Assert.Single(await db.CheckInEvents.Where(e => e.TicketId == _onlineTicket &&
            e.Outcome == "Success" && e.ScannerDeviceId == secondaryIdentity.DeviceId).ToListAsync());
    }

    [SqlServerDomainFact]
    public async Task ExpirySuspensionTransferAndHistoricalReview_KeepTheOriginalEnrollment()
    {
        using var admin = Client(_admin, "Admin");
        using var staff = Client(_staffA);
        var device = await Post<Guid>(admin, Devices, new CreateStationScannerRequest(_stationA, null));
        var old = await Invite(admin, device);
        _now = old.ExpiresAt;
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(Pair, new RedeemScannerInvitationRequest(old.Activation))).StatusCode);
        _now = _now.AddMinutes(-10);

        var paired = await PairDevice(admin, staff, device);
        using var scanner = Client(_staffA, credential: paired.Credential);
        await Post<Guid>(admin, Shifts, new CreateStaffShiftRequest(_staffA, _stationA, _now, _now.AddMinutes(15)));
        await Post<Guid>(admin, $"{CheckIns}/assignments", new CreatePrimaryScannerRequest(device, _callA, _now, _now.AddMinutes(5)));
        var manifest = await scanner.GetFromJsonAsync<ScannerManifestResponse>($"{CheckIns}/manifest/{_callA}");
        var grant = Assert.IsType<ScannerOfflineGrantResponse>(manifest!.OfflineGrant);
        var offline = new CheckInEventInput(_ticket, _callA, Guid.NewGuid().ToString(), _now, null, grant.Id);

        _now = _now.AddSeconds(15);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"{Devices}/{device}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scanner.GetAsync(Status)).StatusCode);
        Assert.Equal("ReviewRequired", (await Sync(scanner, offline)).Outcome);

        var replacement = await Post<Guid>(admin, Devices, new CreateStationScannerRequest(_stationA, null));
        await PairDevice(admin, staff, replacement);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{CheckIns}/assignments",
            new CreatePrimaryScannerRequest(replacement, _callA, _now, _now.AddMinutes(4)))).StatusCode);

        Guid eventId;
        await using (var db = Context())
        {
            var evt = await db.CheckInEvents.SingleAsync(e => e.ClientEventId == offline.ClientEventId);
            Assert.Equal("Conflict", evt.SyncStatus);
            eventId = evt.Id;
        }

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{Devices}/{device}/audit/{eventId}/review",
            new ResolveScannerReviewRequest(true, "Verified gate log."))).StatusCode);
        Assert.Equal("Success", (await Sync(scanner, offline)).Outcome);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{Devices}/{device}/transfer", new TransferScannerRequest(_stationB))).StatusCode);
        var rotated = await PairDevice(admin, staff, device);
        Assert.Equal(device, rotated.Scanner.DeviceId);
        Assert.Equal(_stationB, rotated.Scanner.StationId);
        Assert.NotEqual(paired.Scanner.EnrollmentId, rotated.Scanner.EnrollmentId);
        using var transferred = Client(_staffA, credential: rotated.Credential);
        Assert.Equal("NotAssigned", (await Online(transferred, _onlineTicket, _callA)).Outcome);

        await using var verify = Context();
        var saved = await verify.CheckInEvents.SingleAsync(e => e.Id == eventId);
        Assert.Equal(paired.Scanner.EnrollmentId, saved.ScannerEnrollmentId);
        Assert.Equal(_admin, saved.ReviewedByAccountId);
        var user = await verify.Users.SingleAsync(u => u.Id == _staffA);
        user.IsActive = false;
        await verify.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await transferred.GetAsync(Status)).StatusCode);
    }

    private HttpClient Client(Guid id, string role = "Staff", string? credential = null)
    {
        var client = new HttpClient { BaseAddress = new Uri(_app!.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };
        var jwt = new JwtSecurityToken("WaterbusSystemApi", "WaterbusClients",
            [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        if (credential is not null) client.DefaultRequestHeaders.Add("X-Scanner-Credential", credential);
        return client;
    }

    private Task<CheckInResultResponse> Online(HttpClient scanner, Guid ticketId, Guid callId) =>
        Post<CheckInResultResponse>(scanner, CheckIns, new SubmitCheckInRequest(ticketId, callId, Guid.NewGuid().ToString(), _now, null));

    private static async Task<CheckInEventSyncResultResponse> Sync(HttpClient scanner, CheckInEventInput evt) =>
        Assert.Single((await Post<SyncCheckInEventsResponse>(scanner, $"{CheckIns}/sync", new SyncCheckInEventsRequest([evt]))).Results);

    private static Task<ScannerInvitationResponse> Invite(HttpClient admin, Guid id) =>
        Post<ScannerInvitationResponse>(admin, $"{Devices}/{id}/invitation", new { });

    private static async Task<ScannerPairingResponse> PairDevice(HttpClient admin, HttpClient staff, Guid id) =>
        await Post<ScannerPairingResponse>(staff, Pair, new RedeemScannerInvitationRequest((await Invite(admin, id)).Activation));

    private static async Task<T> Post<T>(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        if (!response.IsSuccessStatusCode)
            Assert.Fail($"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
