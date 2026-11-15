using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Application.Features.Trips.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Sightseeing;

/// <summary>
/// Bằng chứng trên SQL Server thật (ĐÃ được cổng CI "không bị skip" kiểm tra: SightseeingSqlTests): đồ thị
/// Route/Schedule/Trip/TripStopCall được lưu thật, Search/Seat Map/Checkout dùng chung một hành trình Sightseeing,
/// hai khách tranh cùng ghế chỉ một người thắng, thanh toán phát vé với giá Sightseeing và check-in đúng Trip.
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class SightseeingSqlTests : IAsyncLifetime
{
    private const string DbPrefix = "WaterbusDomainTests_Sightseeing_";
    private string? _connection;
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = DbPrefix + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var c = Context();
        await c.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith(DbPrefix))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context();
        await c.Database.EnsureDeletedAsync();
    }

    private static TripService Trips(ApplicationDbContext c) => new(new TripRepository(c), new IncidentRepository(c), new EfUnitOfWork(c),
        Mock.Of<ICurrentUserService>(), new DateTimeService(), Mock.Of<IOperationsAlertPublisher>(), Mock.Of<IUserDirectory>(),
        TestNotifications.Outbox(c), new SqlCommercialTransactionRunner(c));

    /// <summary>Tour vòng A → B → C → A trên Route Sightseeing riêng, cờ lên/xuống dễ dãi (không được hiểu là quyền bán đoạn lẻ).</summary>
    private static Trip SeedCircularTour(ApplicationDbContext c, TestDbContextFactory.TripSeedData seed, int daysAhead = 2)
    {
        var route = new Route
        {
            Code = "TOUR" + Guid.NewGuid().ToString("N")[..4], Name = "Tour vong", DepartureStationId = seed.StationA.Id,
            ArrivalStationId = seed.StationA.Id, EstimatedDurationMinutes = 90, DistanceKm = 12, IsActive = true, ServiceType = "Sightseeing"
        };
        c.Routes.Add(route);
        var stations = new[] { seed.StationA, seed.StationB, seed.StationC, seed.StationA };
        var stops = stations.Select((s, i) => new RouteStop { RouteId = route.Id, StationId = s.Id, SequenceNo = i + 1, CanBoard = true, CanAlight = true }).ToList();
        c.RouteStops.AddRange(stops);
        var departure = DateTimeOffset.UtcNow.AddDays(daysAhead);
        var trip = new Trip
        {
            RouteId = route.Id, BoatId = seed.Boat.Id, TripType = TripType.Sightseeing, Status = TripStatus.Scheduled,
            DepartureTime = departure, ArrivalTime = departure.AddMinutes(90), SalesCloseAt = departure.AddMinutes(-15)
        };
        c.Trips.Add(trip);
        for (var i = 0; i < stops.Count; i++)
            trip.TripStopCalls.Add(new TripStopCall
            {
                TripId = trip.Id, RouteStop = stops[i], VisitOrder = i + 1,
                CheckInOpenAt = departure.AddMinutes(-30 + i * 25), CheckInCloseAt = departure.AddMinutes(-5 + i * 25)
            });
        c.SaveChanges();
        return trip;
    }

    private static TripStopCall Call(Trip t, int order) => t.TripStopCalls.Single(c => c.VisitOrder == order);

    [SqlServerDomainFact]
    public async Task PersistedTour_IsSearchable_AndSeatMapAndPartialJourneysFollowTheSameRule()
    {
        await using var seedContext = Context();
        var seed = TestDbContextFactory.SeedTrip(seedContext);
        var tour = SeedCircularTour(seedContext, seed);
        var mismatched = SeedCircularTour(seedContext, seed, 3);
        (await seedContext.Routes.FindAsync(mismatched.RouteId))!.ServiceType = "Regular"; // loại Route lệch loại Trip
        await seedContext.SaveChangesAsync();

        await using var c = Context();
        var found = await Trips(c).SearchAsync(new SearchTripsRequest(null, null, null, TripType.Sightseeing), default);
        var hit = found.Single(r => r.Id == tour.Id);
        hit.DepartureStationName.Should().Be(hit.ArrivalStationName).And.Be(seed.StationA.Name);
        hit.BasePrice.Should().Be(TestDbContextFactory.SightseeingStandardFare);
        found.Should().NotContain(r => r.Id == mismatched.Id, "Route Regular không được quảng cáo Trip Sightseeing");
        (await Trips(c).SearchAsync(new SearchTripsRequest(seed.StationA.Id, seed.StationB.Id, null, TripType.Sightseeing), default))
            .Should().NotContain(r => r.Id == tour.Id);

        var map = await Trips(c).GetSeatMapAsync(tour.Id, new(Call(tour, 1).Id, Call(tour, 4).Id), default);
        map.Seats.Should().Contain(s => s.Status == "available" && s.Price == TestDbContextFactory.SightseeingStandardFare);
        await Assert.ThrowsAsync<ValidationException>(() =>
            Trips(c).GetSeatMapAsync(tour.Id, new(Call(tour, 1).Id, Call(tour, 2).Id), default));
        await Assert.ThrowsAsync<ValidationException>(() =>
            Trips(c).GetSeatMapAsync(tour.Id, new(Call(tour, 2).Id, Call(tour, 3).Id), default));
    }

    [SqlServerDomainFact]
    public async Task DirectRequests_ForAPartialTour_AreRejected_AndCreateNothing()
    {
        await using var seedContext = Context();
        var seed = TestDbContextFactory.SeedTrip(seedContext);
        var tour = SeedCircularTour(seedContext, seed);

        await using var c = Context();
        var checkout = CommercialHandlerFactory.Checkout(c, new SqlCommercialTransactionRunner(c));
        await Assert.ThrowsAsync<ValidationException>(() => checkout.HoldAsync(new([new(tour.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationB.Id, Call(tour, 1).Id, Call(tour, 2).Id)])));
        await Assert.ThrowsAsync<ValidationException>(() => CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c))
            .CreateAsync(new CreateBookingRequest(tour.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationB.Id, "Buyer", "buyer@example.com", null)));
        await using var check = Context();
        (await check.SeatReservations.CountAsync()).Should().Be(0);
        (await check.Bookings.CountAsync()).Should().Be(0);
        (await check.PurchaseOrders.CountAsync()).Should().Be(0);
    }

    [SqlServerDomainFact]
    public async Task TwoCustomersRacingForTheSameSeat_OnTheTour_OnlyOneWins()
    {
        await using var seedContext = Context();
        var seed = TestDbContextFactory.SeedTrip(seedContext);
        var tour = SeedCircularTour(seedContext, seed);
        var request = new CreateBookingRequest(tour.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationA.Id, "Buyer", "buyer@example.com",
            null, null, Call(tour, 1).Id, Call(tour, 4).Id);

        async Task<bool> Attempt()
        {
            await using var c = Context();
            try { await CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c)).CreateAsync(request, default); return true; }
            catch (ConcurrencyException) { return false; }
        }

        var results = await Task.WhenAll(Attempt(), Attempt());
        results.Count(x => x).Should().Be(1);
        await using var check = Context();
        var reservation = await check.SeatReservations.SingleAsync();
        (reservation.BoardingStopOrder, reservation.DisembarkingStopOrder).Should().Be((1, 4));
    }

    [SqlServerDomainFact]
    public async Task PaidTour_IssuesTicketWithSightseeingFare_AndCheckInIsOnlyAtThatTripsBoardingCall()
    {
        var staffId = Guid.NewGuid();
        await using var c = Context();
        var seed = TestDbContextFactory.SeedTrip(c);
        var tour = SeedCircularTour(c, seed);
        var runner = new SqlCommercialTransactionRunner(c);

        // OneWay checkout có chuyến Sightseeing: một Order một Booking, giá chỉ theo TripType x SeatClass.
        var sessions = new CheckoutSessionService(new CommercialHandlerFactory.MemoryStore(), new DateTimeService(),
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));
        var checkoutResult = await CommercialHandlerFactory.Checkout(c, runner, sessions).CreateAsync(new("Buyer", "buyer@example.com", null,
            [new(tour.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationA.Id, null, Call(tour, 1).Id, Call(tour, 4).Id)]), default);
        var order = await c.PurchaseOrders.Include(o => o.Bookings).SingleAsync();
        order.Bookings.Should().ContainSingle();
        order.QuotedTotal.Should().Be(TestDbContextFactory.SightseeingStandardFare);
        checkoutResult.Should().NotBeNull();

        c.FinancialTransactions.Add(new FinancialTransaction
        {
            Order = order, OrderId = order.Id, Amount = order.QuotedTotal, GatewayName = "VNPAY", MerchantReference = "sight-payment",
            IdempotencyKey = "PAY_sight", GatewayRequestCreatedAt = DateTimeOffset.UtcNow
        });
        await c.SaveChangesAsync();
        var payment = await c.FinancialTransactions.SingleAsync();
        (await CommercialHandlerFactory.Ipn(c, new CommercialCheckoutTests.Gateway(), transactions: runner)
            .ConfirmIpnAsync(CommercialCheckoutTests.Ipn(payment), default)).RspCode.Should().Be("00");

        await using var verify = Context();
        var ticket = await verify.Tickets.Include(t => t.Booking).SingleAsync();
        ticket.FareTripTypeSnapshot.Should().Be("Sightseeing");
        ticket.PaidFareSnapshot.Should().Be(TestDbContextFactory.SightseeingStandardFare);
        ticket.Booking!.BoardingCallId.Should().Be(Call(tour, 1).Id);
        ticket.Booking.DisembarkingCallId.Should().Be(Call(tour, 4).Id);
        (await verify.PurchaseOrders.SingleAsync()).Status.Should().Be(OrderStatus.Confirmed);

        // Check-in: Staff được phân công ĐÚNG điểm lên của chuyến tham quan; vé không check-in được ở Trip Regular cùng bến.
        var device = new ScannerDevice { DeviceCode = "DEV-SS", IsActive = true };
        verify.ScannerDevices.Add(device);
        var boardingCall = await verify.TripStopCalls.SingleAsync(x => x.Id == Call(tour, 1).Id);
        var regularCall = await verify.TripStopCalls.FirstAsync(x => x.TripId == seed.Trip.Id && x.VisitOrder == 1);
        foreach (var call in new[] { boardingCall, regularCall })
            verify.ScannerAssignments.Add(new ScannerAssignment
            {
                DeviceId = device.Id, StaffAccountId = staffId, TripStopCallId = call.Id, IsPrimaryOffline = call == boardingCall,
                ActiveFrom = tour.DepartureTime.AddHours(-2), ActiveUntil = tour.DepartureTime.AddDays(1)
            });
        await verify.SaveChangesAsync();

        var now = boardingCall.CheckInOpenAt!.Value.AddMinutes(5);
        CheckInService Service(ApplicationDbContext ctx)
        {
            var user = new Mock<ICurrentUserService>();
            user.Setup(x => x.UserId).Returns(staffId.ToString());
            user.Setup(x => x.UserRole).Returns("Staff");
            var clock = new Mock<IDateTimeService>();
            clock.Setup(x => x.Now).Returns(now);
            var repository = new CheckInRepository(ctx);
            return new CheckInService(repository, new EfUnitOfWork(ctx), user.Object, clock.Object, new CheckInProcessor(repository),
                new SqlScannerAssignmentLock(ctx), Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);
        }

        await using var scan = Context();
        var wrong = await Service(scan).SyncCheckInEventsAsync(new([new(ticket.Id, regularCall.Id, "evt-wrong-" + Guid.NewGuid(), now, null)]), default);
        wrong.Results.Single().Outcome.Should().Be("InvalidStop", "vé Sightseeing không check-in được tại điểm dừng của Trip Regular");
        var right = await Service(scan).SyncCheckInEventsAsync(new([new(ticket.Id, boardingCall.Id, "evt-right-" + Guid.NewGuid(), now, null)]), default);
        right.Results.Single().Outcome.Should().Be("Success");
        await using var final = Context();
        (await final.Tickets.SingleAsync()).BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
    }

    [SqlServerDomainFact]
    public async Task DemoSeed_OnSqlServer_IsIdempotent_AndItsCircularTourIsGenuinelyBookable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(_connection!));
        services.AddIdentity<ApplicationUser, ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<ApplicationDbContextInitializer>();
        await using var provider = services.BuildServiceProvider();
        for (var i = 0; i < 2; i++)
        {
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>().SeedAsync();
        }

        await using var c = Context();
        var trips = await c.Trips.Include(t => t.Route).Include(t => t.Schedule).Where(t => t.ScheduleId != null).ToListAsync();
        trips.Should().OnlyContain(t => t.TripType == t.Schedule!.TripType);
        trips.Should().OnlyContain(t => t.Route!.ServiceType == (t.TripType == TripType.Sightseeing ? "Sightseeing" : "Regular"));
        (await c.Trips.CountAsync()).Should().Be(trips.Count);

        var found = await Trips(c).SearchAsync(new SearchTripsRequest(null, null, null, TripType.Sightseeing), default);
        found.Should().NotBeEmpty("seed phải có chuyến Sightseeing tương lai bán được");
        var circular = found.First(f => f.DepartureStationName == f.ArrivalStationName);
        var tourCalls = await c.TripStopCalls.Where(x => x.TripId == circular.Id).OrderBy(x => x.VisitOrder).ToListAsync();
        tourCalls.Should().HaveCount(4);

        var map = await Trips(c).GetSeatMapAsync(circular.Id, new(tourCalls[0].Id, tourCalls[^1].Id), default);
        var seat = map.Seats.First(s => s.Status == "available");
        var station = (await c.RouteStops.FindAsync(tourCalls[0].RouteStopId))!.StationId;
        var booking = await CommercialHandlerFactory.Booking(c, transactions: new SqlCommercialTransactionRunner(c)).CreateAsync(
            new CreateBookingRequest(circular.Id, [seat.SeatId], station, station, "Buyer", "buyer@example.com", null,
                null, tourCalls[0].Id, tourCalls[^1].Id));
        booking.TotalAmount.Should().Be(seat.Price);
        (await c.Bookings.SingleAsync()).BoardingCallId.Should().Be(tourCalls[0].Id);

        // Search không còn chuyến Regular nào mang nhãn Sightseeing.
        (await c.Trips.CountAsync(t => t.TripType == TripType.Sightseeing && t.Route!.ServiceType == "Regular")).Should().Be(0);
    }
}
