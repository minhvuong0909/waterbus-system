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

namespace WaterbusSystem.UnitTests.Trips;

/// <summary>
/// Task 6 — bằng chứng trên SQL Server thật + HTTP thật: Trip ad-hoc (BR-SCH-10), hợp đồng Search có chặn,
/// đổi tàu có ghế đã bán (BR-SCH-07), cập nhật TotalSeats đồng thời với kích hoạt ghế.
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class ApiReliabilitySqlHttpTests : IAsyncLifetime
{
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
        { InitialCatalog = "WaterbusDomainTests_Polish_" + Guid.NewGuid().ToString("N") }.ConnectionString;
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
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminTripsController).Assembly);
        builder.Services.AddRateLimiter(o => o.AddPolicy("BookingPolicy", _ => RateLimitPartition.GetNoLimiter("t")));
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.MapControllers();
        await _app.StartAsync();

        await using var seed = Context();
        _seed = TestDbContextFactory.SeedTrip(seed);
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_Polish_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context();
        await c.Database.EnsureDeletedAsync();
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

    // ───────────────────────── BR-SCH-10: Trip ad-hoc ─────────────────────────

    private async Task<(Guid StopA, Guid StopB, Guid StopC)> RouteStopsAsync()
    {
        await using var c = Context();
        var stops = await c.RouteStops.Where(s => s.RouteId == _seed.Route.Id).OrderBy(s => s.SequenceNo).Select(s => s.Id).ToListAsync();
        return (stops[0], stops[1], stops[2]);
    }

    private object AdHocBody((Guid A, Guid B, Guid C) s, string tripType = "Commuter", DateTimeOffset? departure = null, Guid? routeId = null) => new
    {
        routeId = routeId ?? _seed.Route.Id, tripType, departureTime = departure ?? DateTimeOffset.UtcNow.AddDays(2),
        stops = new[]
        {
            new { routeStopId = s.A, arrivalOffsetMin = (int?)null, departureOffsetMin = (int?)0 },
            new { routeStopId = s.B, arrivalOffsetMin = (int?)10, departureOffsetMin = (int?)12 },
            new { routeStopId = s.C, arrivalOffsetMin = (int?)25, departureOffsetMin = (int?)null }
        },
        salesCloseMinutesBeforeDeparture = 30, checkInOpenMinutesBeforeDeparture = 45
    };

    [SqlServerDomainFact]
    public async Task AdHocTrip_IsAdminOnly_ValidatesBusinessRules_AndIsNotSellableUntilABoatIsAssigned()
    {
        var (a, b, c) = await RouteStopsAsync();
        var body = AdHocBody((a, b, c));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Http().PostAsJsonAsync("/api/v1/admin/trips", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Http("Passenger").PostAsJsonAsync("/api/v1/admin/trips", body)).StatusCode);
        var admin = Http("Admin");

        // 400: giờ quá khứ / TripType sai / bến cuối có giờ rời / chỉ 1 bến; 404: Route không có; 409: loại dịch vụ lệch Route
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/trips", AdHocBody((a, b, c), departure: DateTimeOffset.UtcNow.AddMinutes(-5)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/trips", AdHocBody((a, b, c), tripType: "Teleport"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/trips", new
        { routeId = _seed.Route.Id, tripType = "Commuter", departureTime = DateTimeOffset.UtcNow.AddDays(2),
            stops = new[] { new { routeStopId = a, arrivalOffsetMin = (int?)null, departureOffsetMin = (int?)0 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/trips", new
        { routeId = _seed.Route.Id, tripType = "Commuter", departureTime = DateTimeOffset.UtcNow.AddDays(2),
            stops = new[] { new { routeStopId = a, arrivalOffsetMin = (int?)null, departureOffsetMin = (int?)0 },
                            new { routeStopId = Guid.NewGuid(), arrivalOffsetMin = (int?)20, departureOffsetMin = (int?)null } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/v1/admin/trips", AdHocBody((a, b, c), routeId: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/trips", AdHocBody((a, b, c), tripType: "Sightseeing"))).StatusCode);
        await using (var check = Context()) Assert.Equal(1, await check.Trips.CountAsync());   // chỉ Trip seed, các lần lỗi không ghi gì

        var departure = DateTimeOffset.UtcNow.AddDays(2);
        var created = await admin.PostAsJsonAsync("/api/v1/admin/trips", AdHocBody((a, b, c), departure: departure));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tripId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tripId").GetGuid();

        await using (var db = Context())
        {
            var trip = await db.Trips.Include(t => t.TripStopCalls).SingleAsync(t => t.Id == tripId);
            Assert.Null(trip.ScheduleId);
            Assert.Null(trip.BoatId);
            Assert.Equal(TripStatus.Scheduled, trip.Status);
            Assert.Equal(departure.AddMinutes(25), trip.ArrivalTime);
            Assert.Equal(departure.AddMinutes(-30), trip.SalesCloseAt);
            Assert.Equal([1, 2, 3], trip.TripStopCalls.OrderBy(x => x.VisitOrder).Select(x => x.VisitOrder).ToArray());
            Assert.Equal(departure.AddMinutes(-45), trip.TripStopCalls.Single(x => x.VisitOrder == 1).CheckInOpenAt);
        }

        // chưa có tàu ⇒ không xuất hiện trong tìm kiếm (BR-SCH-04); gán tàu xong thì bán được như Trip thường
        var date = departure.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");
        string[] Ids(JsonElement e) => e.EnumerateArray().Select(x => x.GetProperty("id").GetString()!).ToArray();
        Assert.DoesNotContain(tripId.ToString(), Ids(await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trips/search?departureDate={date}")), StringComparer.OrdinalIgnoreCase);
        var assign = await admin.PutAsJsonAsync($"/api/v1/admin/trips/{tripId}/boat", new { boatId = _seed.Boat.Id });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        Assert.Contains(tripId.ToString(), Ids(await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trips/search?departureDate={date}")), StringComparer.OrdinalIgnoreCase);
    }

    [SqlServerDomainFact]
    public async Task AdHocTrip_RejectsNullStopsLateFirstDepartureAndNonBoardableEnds_WithCleanProblemDetails()
    {
        var (a, b, c) = await RouteStopsAsync();
        var admin = Http("Admin");
        var when = DateTimeOffset.UtcNow.AddDays(2);
        // phần tử stop null → 400 (không phải 500)
        var withNull = await admin.PostAsync("/api/v1/admin/trips", new StringContent(
            "{\"routeId\":\"" + _seed.Route.Id + "\",\"tripType\":\"Commuter\",\"departureTime\":\"" + when.ToString("O") + "\",\"stops\":[null,{\"routeStopId\":\"" + b + "\",\"arrivalOffsetMin\":20}]}",
            Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, withNull.StatusCode);
        // bến đầu rời muộn hơn giờ khởi hành của chuyến → 400
        var late = await admin.PostAsJsonAsync("/api/v1/admin/trips", new
        { routeId = _seed.Route.Id, tripType = "Commuter", departureTime = when,
            stops = new[] { new { routeStopId = a, arrivalOffsetMin = (int?)null, departureOffsetMin = (int?)60 },
                            new { routeStopId = c, arrivalOffsetMin = (int?)90, departureOffsetMin = (int?)null } } });
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        using (var problem = JsonDocument.Parse(await late.Content.ReadAsStringAsync()))
        {
            Assert.True(problem.RootElement.TryGetProperty("errors", out _));
            Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        }
        // bến đầu không cho lên → 400
        await using (var db = Context())
        {
            (await db.RouteStops.SingleAsync(s => s.Id == a)).CanBoard = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/trips", AdHocBody((a, b, c)))).StatusCode);
    }

    // ───────────────────────── Search: lọc bến trước khi cắt ứng viên ─────────────────────────

    [SqlServerDomainFact]
    public async Task Search_StationFilterIsAppliedBeforeTheCandidateCap_SoUnrelatedTripsCannotHideMatches()
    {
        await using var c = Context();
        var otherRoute = new Route { Code = "RT-OTHER", Name = "Tuyen khac", DepartureStationId = _seed.StationA.Id, ArrivalStationId = _seed.StationB.Id,
            EstimatedDurationMinutes = 10, DistanceKm = 1, IsActive = true };
        c.Routes.Add(otherRoute);
        var s1 = new RouteStop { RouteId = otherRoute.Id, StationId = _seed.StationA.Id, SequenceNo = 1 };
        var s2 = new RouteStop { RouteId = otherRoute.Id, StationId = _seed.StationB.Id, SequenceNo = 2 };
        c.RouteStops.AddRange(s1, s2);
        var match = await c.Trips.SingleAsync();
        match.DepartureTime = DateTimeOffset.UtcNow.AddDays(10);   // chuyến khớp (A→C) nằm SAU mọi chuyến gây nhiễu
        match.ArrivalTime = match.DepartureTime.AddMinutes(30);
        match.SalesCloseAt = match.DepartureTime;
        for (var i = 0; i < 1005; i++)                              // 1005 chuyến A→B sớm hơn, chiếm hết giới hạn 1000 nếu không lọc bến
        {
            var dep = DateTimeOffset.UtcNow.AddHours(2).AddMinutes(i);
            var trip = new Trip { RouteId = otherRoute.Id, BoatId = _seed.Boat.Id, TripType = TripType.Commuter, Status = TripStatus.Scheduled,
                DepartureTime = dep, ArrivalTime = dep.AddMinutes(10), SalesCloseAt = dep };
            trip.TripStopCalls.Add(new TripStopCall { TripId = trip.Id, RouteStopId = s1.Id, VisitOrder = 1 });
            trip.TripStopCalls.Add(new TripStopCall { TripId = trip.Id, RouteStopId = s2.Id, VisitOrder = 2 });
            c.Trips.Add(trip);
        }
        await c.SaveChangesAsync();
        var result = await Http().GetFromJsonAsync<JsonElement>($"/api/v1/trips/search?departureStationId={_seed.StationA.Id}&arrivalStationId={_seed.StationC.Id}");
        Assert.Equal(match.Id.ToString(), Assert.Single(result.EnumerateArray()).GetProperty("id").GetString(), ignoreCase: true);
    }

    // ───────────────────────── BR-SCH-07: đổi tàu với ghế đã bán ─────────────────────────

    [SqlServerDomainFact]
    public async Task BoatReplacement_RemapsSoldSeatsToSameCodeAndClass_AndRejectsIncompatibleBoat()
    {
        await using (var c = Context())
        {
            var booking = CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c));
            await booking.CreateAsync(CommercialCheckoutTests.Command(_seed), default);
            // tàu thay thế tương thích: cùng mã ghế + cùng hạng
            var replacement = new Boat { Code = "SWB-REPL", Name = "Repl", RegistrationNumber = "REG-REPL", TotalSeats = 2, Manufacturer = "T", YearBuilt = 2024, IsActive = true };
            replacement.Seats.Add(new Seat { BoatId = replacement.Id, SeatClassId = _seed.StandardClass.Id, SeatCode = "S01", RowNumber = 1, ColumnNumber = 1, IsActive = true });
            replacement.Seats.Add(new Seat { BoatId = replacement.Id, SeatClassId = _seed.StandardClass.Id, SeatCode = "S02", RowNumber = 1, ColumnNumber = 2, IsActive = true });
            c.Boats.Add(replacement);
            await c.SaveChangesAsync();
        }
        await using (var c = Context())
        {
            // đánh dấu là ghế ĐÃ BÁN (Confirmed) để BR-SCH-07 được thử với dữ liệu đã thanh toán, không chỉ ghế đang giữ
            var sold = await c.SeatReservations.SingleAsync();
            sold.Status = ReservationStatus.Confirmed;
            await c.SaveChangesAsync();
        }
        var admin = Http("Admin");
        // OtherBoat chỉ có ghế X01 ⇒ thiếu S01 → 409, đặt chỗ không đổi
        var incompatible = await admin.PutAsJsonAsync($"/api/v1/admin/trips/{_seed.Trip.Id}/boat", new { boatId = _seed.OtherBoat.Id });
        Assert.Equal(HttpStatusCode.Conflict, incompatible.StatusCode);
        using (var problem = JsonDocument.Parse(await incompatible.Content.ReadAsStringAsync()))
        {
            Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
            Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        }
        await using (var c = Context())
        {
            var r = await c.SeatReservations.Include(x => x.Seat).SingleAsync();
            Assert.Equal(_seed.Seat1.Id, r.SeatId);
            Assert.Equal(_seed.Boat.Id, (await c.Trips.SingleAsync(t => t.Id == _seed.Trip.Id)).BoatId);
        }

        Guid replacementId;
        await using (var c = Context()) replacementId = (await c.Boats.SingleAsync(b => b.Code == "SWB-REPL")).Id;
        var ok = await admin.PutAsJsonAsync($"/api/v1/admin/trips/{_seed.Trip.Id}/boat", new { boatId = replacementId });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(1, (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("remappedReservations").GetInt32());
        await using (var c = Context())
        {
            var r = await c.SeatReservations.Include(x => x.Seat).SingleAsync();
            Assert.Equal(replacementId, r.Seat!.BoatId);
            Assert.Equal("S01", r.Seat.SeatCode);
            Assert.Equal(_seed.StandardClass.Id, r.Seat.SeatClassId);
            Assert.Equal(replacementId, (await c.Trips.SingleAsync(t => t.Id == _seed.Trip.Id)).BoatId);
        }
    }

    // ───────────────────────── Search có chặn ─────────────────────────

    [SqlServerDomainFact]
    public async Task Search_HasADefaultPageSize_AWindowWhenNoDateGiven_AndRejectsOversizedPages()
    {
        await using (var c = Context())
        {
            var stops = await c.RouteStops.Where(s => s.RouteId == _seed.Route.Id).OrderBy(s => s.SequenceNo).ToListAsync();
            var template = await c.Trips.SingleAsync();
            template.IsDeleted = true;   // loại Trip seed khỏi phép đếm
            for (var i = 0; i < 130; i++)
            {
                var departure = DateTimeOffset.UtcNow.AddHours(2 + i);   // 130 chuyến trong 5,4 ngày
                var trip = new Trip { RouteId = _seed.Route.Id, BoatId = _seed.Boat.Id, TripType = TripType.Commuter, Status = TripStatus.Scheduled,
                    DepartureTime = departure, ArrivalTime = departure.AddMinutes(30), SalesCloseAt = departure };
                for (var v = 0; v < stops.Count; v++)
                    trip.TripStopCalls.Add(new TripStopCall { TripId = trip.Id, RouteStopId = stops[v].Id, VisitOrder = v + 1 });
                c.Trips.Add(trip);
            }
            // một chuyến ngoài cửa sổ mặc định 14 ngày
            var far = DateTimeOffset.UtcNow.AddDays(30);
            var farTrip = new Trip { RouteId = _seed.Route.Id, BoatId = _seed.Boat.Id, TripType = TripType.Commuter, Status = TripStatus.Scheduled,
                DepartureTime = far, ArrivalTime = far.AddMinutes(30), SalesCloseAt = far };
            for (var v = 0; v < stops.Count; v++)
                farTrip.TripStopCalls.Add(new TripStopCall { TripId = farTrip.Id, RouteStopId = stops[v].Id, VisitOrder = v + 1 });
            c.Trips.Add(farTrip);
            await c.SaveChangesAsync();
            FarTripId = farTrip.Id;
        }
        var http = Http();
        var page1 = await http.GetFromJsonAsync<JsonElement>("/api/v1/trips/search");
        Assert.Equal(100, page1.GetArrayLength());                                  // mặc định pageSize = 100, không còn "trả hết"
        var page2 = await http.GetFromJsonAsync<JsonElement>("/api/v1/trips/search?page=2");
        Assert.Equal(30, page2.GetArrayLength());
        Assert.DoesNotContain(FarTripId.ToString(), page1.EnumerateArray().Concat(page2.EnumerateArray())
            .Select(x => x.GetProperty("id").GetString()!), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(5, (await http.GetFromJsonAsync<JsonElement>("/api/v1/trips/search?pageSize=5")).GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/v1/trips/search?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/v1/trips/search?page=0")).StatusCode);
        // ngày tường minh vẫn tìm được chuyến xa
        var farDate = DateTimeOffset.UtcNow.AddDays(30).ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");
        Assert.Contains(FarTripId.ToString(), (await http.GetFromJsonAsync<JsonElement>($"/api/v1/trips/search?departureDate={farDate}"))
            .EnumerateArray().Select(x => x.GetProperty("id").GetString()!), StringComparer.OrdinalIgnoreCase);
    }
    private Guid FarTripId;

    // ───────────────────────── Boat.TotalSeats vs kích hoạt ghế đồng thời ─────────────────────────

    [SqlServerDomainFact]
    public async Task TotalSeatsUpdateRacingSeatActivation_NeverBreaksTheCapacityInvariant_AndLosersGetCleanRejections()
    {
        for (var round = 0; round < 6; round++)
        {
            Guid boatId, seatId;
            await using (var c = Context())
            {
                var boat = new Boat { Code = "RACE" + round, Name = "Race", RegistrationNumber = "RACE-REG" + round, TotalSeats = 3, Manufacturer = "T", YearBuilt = 2024, IsActive = true };
                boat.Seats.Add(new Seat { BoatId = boat.Id, SeatClassId = _seed.StandardClass.Id, SeatCode = "A1", RowNumber = 1, ColumnNumber = 1, IsActive = true });
                boat.Seats.Add(new Seat { BoatId = boat.Id, SeatClassId = _seed.StandardClass.Id, SeatCode = "A2", RowNumber = 1, ColumnNumber = 2, IsActive = true });
                var spare = new Seat { BoatId = boat.Id, SeatClassId = _seed.StandardClass.Id, SeatCode = "A3", RowNumber = 1, ColumnNumber = 3, IsActive = false };
                boat.Seats.Add(spare);
                c.Boats.Add(boat);
                await c.SaveChangesAsync();
                boatId = boat.Id; seatId = spare.Id;
            }
            using var gate = new Barrier(2);
            async Task<Exception?> Run(Func<ApplicationDbContext, Task> action)
            {
                await using var c = Context();
                gate.SignalAndWait();
                try { await action(c); return null; } catch (Exception ex) { return ex; }
            }
            var results = await Task.WhenAll(
                Task.Run(() => Run(c => new BoatService(new BoatRepository(c), new EfUnitOfWork(c), new SqlCommercialTransactionRunner(c))
                    .UpdateAsync(boatId, new UpdateBoatRequest("Race", "RACE-REG" + round, 2, "T", 2024, true, null)))),
                Task.Run(() => Run(c => new BoatSeatService(new BoatSeatRepository(c), new EfUnitOfWork(c), new SqlCommercialTransactionRunner(c))
                    .ActivateSeatAsync(boatId, seatId))));

            await using var check = Context();
            var total = (await check.Boats.AsNoTracking().SingleAsync(b => b.Id == boatId)).TotalSeats;
            var active = await check.Seats.CountAsync(s => s.BoatId == boatId && s.IsActive);
            Assert.True(active <= total, $"round {round}: {active} ghế hoạt động > TotalSeats {total}");
            // Tuần tự hoá bằng khoá thương mại: đúng một thao tác thành công, thao tác kia bị từ chối sạch (cả hai thứ tự đều dẫn tới 3 ghế > TotalSeats 2).
            Assert.Equal(1, results.Count(e => e is null));
            var loser = results.Single(e => e is not null)!;
            Assert.True(loser is ConflictException or ConcurrencyException, $"round {round}: lỗi thô {loser.GetType().Name}");
            Assert.True((total == 2 && active == 2) || (total == 3 && active == 3), $"round {round}: trạng thái không khớp bên thắng ({total}/{active})");
        }
    }


    // ───────────────────────── IP cho VNPay ─────────────────────────

    [Theory]
    [InlineData("::ffff:172.18.0.1", "172.18.0.1")]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    public void ClientIp_MapsIpv4MappedAddresses(string input, string expected) =>
        Assert.Equal(expected, BaseApiController.ToIpv4String(System.Net.IPAddress.Parse(input)));

    [Fact]
    public void ClientIp_FallsBackToLoopbackWhenUnknown() => Assert.Equal("127.0.0.1", BaseApiController.ToIpv4String(null));
}
