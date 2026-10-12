using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using Xunit;

namespace WaterbusSystem.UnitTests.Domain;

public sealed class SqlServerDomainFactAttribute : FactAttribute
{
    public SqlServerDomainFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER")))
            Skip = "Set WATERBUS_TEST_SQLSERVER to a local SQL Server connection; tests use a separate temporary database.";
    }
}

[CollectionDefinition("Domain SQL Server")]
public sealed class DomainSqlCollection : ICollectionFixture<DomainSqlFixture> { }

public sealed class DomainSqlFixture : IAsyncLifetime
{
    private string? _connectionString;
    public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(_connectionString ?? throw new InvalidOperationException("SQL test connection is missing."))
        .Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var connection = new SqlConnectionStringBuilder(configured)
        { InitialCatalog = "WaterbusDomainTests_" + Guid.NewGuid().ToString("N") };
        _connectionString = connection.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connectionString == null) return;
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}

[Collection("Domain SQL Server")]
public class DomainPersistenceTests(DomainSqlFixture fixture)
{
    [SqlServerDomainFact]
    public async Task Migrations_UpgradeDowngradeAndReapplyOnSqlServer()
    {
        await using var context = fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261003095819_Phase3_DomainCompletion");
        await migrator.MigrateAsync();
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [SqlServerDomainFact]
    public async Task Booking_CannotReferenceABoardingCallOnAnotherTrip()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var (booking, otherTrip, _) = await SeedJourney(context);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        // Domain validation is deliberately bypassed to prove the database FK itself.
        var act = () => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Bookings] SET [TripId] = {otherTrip.Id} WHERE [Id] = {booking.Id}");
        await act.Should().ThrowAsync<SqlException>();
    }

    [SqlServerDomainFact]
    public async Task Reservation_CannotUseATripDifferentFromItsBooking()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var (booking, otherTrip, seat) = await SeedJourney(context);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        context.SeatReservations.Add(new SeatReservation
        {
            BookingId = booking.Id, TripId = otherTrip.Id, SeatId = seat.Id,
            PassengerName = "Passenger", BoardingStopOrder = 1, DisembarkingStopOrder = 2
        });
        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SqlServerDomainFact]
    public async Task Ticket_CannotReferenceAReservationOwnedByAnotherBooking()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var (booking, _, seat) = await SeedJourney(context);
        var otherBooking = new Booking { Order = booking.Order, TripId = booking.TripId };
        otherBooking.SetJourney(booking.BoardingCall!, booking.DisembarkingCall!);
        context.Bookings.AddRange(booking, otherBooking);
        var reservation = new SeatReservation
        {
            Booking = booking, TripId = booking.TripId, SeatId = seat.Id,
            PassengerName = "Passenger", BoardingStopOrder = 1, DisembarkingStopOrder = 2
        };
        context.SeatReservations.Add(reservation);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.Tickets.Add(new Ticket
        { BookingId = otherBooking.Id, SeatReservationId = reservation.Id, TicketCode = Guid.NewGuid().ToString("N") });
        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SqlServerDomainFact]
    public async Task Refund_CannotReferenceAPaymentOfAnotherOrder()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var (booking, _, _) = await SeedJourney(context);
        context.Bookings.Add(booking);
        var payment = new FinancialTransaction
        {
            Order = new PurchaseOrder(), Amount = 500000m,
            Status = FinancialTransactionStatus.Succeeded, IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        context.FinancialTransactions.Add(payment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.FinancialTransactions.Add(new FinancialTransaction
        {
            OrderId = booking.OrderId, RefundBookingId = booking.Id, OriginalPaymentId = payment.Id,
            TransactionType = FinancialTransactionType.Refund, Amount = 200000m,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        });
        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SqlServerDomainFact]
    public async Task ManualRefund_CannotSucceedWithoutEvidenceAndAdmin()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var (booking, _, _) = await SeedJourney(context);
        context.Bookings.Add(booking);
        var payment = new FinancialTransaction
        {
            Order = booking.Order, Amount = 500000m,
            Status = FinancialTransactionStatus.Succeeded, IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        context.FinancialTransactions.Add(payment);
        await context.SaveChangesAsync();
        context.FinancialTransactions.Add(new FinancialTransaction
        {
            OrderId = booking.OrderId, RefundBookingId = booking.Id, OriginalPaymentId = payment.Id,
            TransactionType = FinancialTransactionType.Refund, Amount = 200000m,
            GatewayName = "Manual", Status = FinancialTransactionStatus.Succeeded,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        });
        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SqlServerDomainFact]
    public async Task ValidJourney_RoundTripsAndSeatReusePersistWithoutPrematureTickets()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var (booking, _, seat) = await SeedJourney(context);
        var order = booking.Order!;
        order.PurchaseMode = PurchaseMode.RoundTrip;
        var middle = new TripStopCall
        { TripId = booking.TripId, RouteStopId = booking.BoardingCall!.RouteStopId, VisitOrder = 2 };
        context.TripStopCalls.Add(middle);
        await context.SaveChangesAsync();
        var secondBooking = new Booking { Order = order, OrderId = order.Id, QuotedTotal = 15000m };
        secondBooking.SetJourney(middle, booking.DisembarkingCall!);
        booking.SetJourney(booking.BoardingCall!, middle);
        booking.QuotedTotal = 15000m;
        order.QuotedTotal = 30000m;
        order.Bookings.Add(booking);
        order.Bookings.Add(secondBooking);
        order.ValidateForCheckout();
        context.PurchaseOrders.Add(order);
        // Duplicate seat is legal for distinct segments. No UNIQUE(TripId, SeatId).
        context.SeatReservations.AddRange(
            new SeatReservation { Booking = booking, TripId = booking.TripId, SeatId = seat.Id,
                PassengerName = "First passenger", BoardingStopOrder = 1, DisembarkingStopOrder = 2 },
            new SeatReservation { Booking = secondBooking, TripId = booking.TripId, SeatId = seat.Id,
                PassengerName = "Second passenger", BoardingStopOrder = 2, DisembarkingStopOrder = 3 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var stored = await context.PurchaseOrders.Include(o => o.Bookings)
            .ThenInclude(b => b.SeatReservations).SingleAsync();
        stored.PassengerAccountId.Should().BeNull();
        stored.Bookings.Should().HaveCount(2);
        stored.Bookings.SelectMany(b => b.SeatReservations).Should().HaveCount(2);
        (await context.Tickets.CountAsync()).Should().Be(0);
    }

    [SqlServerDomainFact]
    public async Task Ticket_IssuancePersistsSnapshotsAndRejectsDuplicateReservation()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var (booking, _, seat) = await SeedJourney(context);
        booking.Status = BookingStatus.Confirmed;
        booking.QuotedTotal = 15000m;
        booking.PaidAllocation = 15000m;
        context.Bookings.Add(booking);
        var reservation = new SeatReservation
        {
            Booking = booking, BookingId = booking.Id, TripId = booking.TripId, SeatId = seat.Id,
            PassengerName = "Passenger", SeatCodeAtSale = seat.SeatCode, SeatClassAtSale = "Standard",
            BoardingStopOrder = 1, DisembarkingStopOrder = 3,
            QuotedFare = 15000m, PaidAllocation = 15000m, Status = ReservationStatus.Confirmed
        };
        var payment = new FinancialTransaction
        {
            Order = booking.Order, OrderId = booking.OrderId, Amount = 15000m,
            Status = FinancialTransactionStatus.Succeeded, IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        context.AddRange(reservation, payment);
        var ticket = Ticket.Issue(booking, reservation, payment, TripType.Sightseeing, DateTimeOffset.UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var stored = await context.Tickets.SingleAsync();
        stored.PaidFareSnapshot.Should().Be(15000m);
        stored.FareSeatClassSnapshot.Should().Be("Standard");
        stored.BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
        context.ChangeTracker.Clear();
        context.Tickets.Add(new Ticket
        { BookingId = booking.Id, SeatReservationId = reservation.Id, TicketCode = Guid.NewGuid().ToString("N") });
        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SqlServerDomainFact]
    public async Task Migration_RejectsUnmappedLegacyBookingsWithoutLosingData()
    {
        var isolated = new DomainSqlFixture();
        try
        {
            await isolated.InitializeAsync();
            await using var context = isolated.CreateContext();
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20261003095819_Phase3_DomainCompletion");
            await context.Database.ExecuteSqlRawAsync("""
                DECLARE @order uniqueidentifier = NEWID();
                INSERT INTO PurchaseOrders (Id, PurchaserName, PurchaserEmail, PurchaseMode, Status, QuotedTotal, Currency, CreatedAt, IsDeleted)
                VALUES (@order, 'Purchaser', 'p@example.com', 'OneWay', 'Pending', 15000, 'VND', SYSDATETIMEOFFSET(), 0);
                INSERT INTO Bookings (Id, OrderId, BookingCode, PublicBookingId, QrCredentialVersion, CustomerName, CustomerEmail,
                    TotalAmount, Status, PaymentStatus, ManageOrderTokenRevoked, CreatedAt, IsDeleted)
                VALUES (NEWID(), @order, 'LEGACY', 'legacy-public', 1, 'Purchaser', 'p@example.com', 15000, 1, 1, 0, SYSDATETIMEOFFSET(), 0);
                """);
            var act = () => migrator.MigrateAsync();
            var failure = await act.Should().ThrowAsync<SqlException>();
            failure.Which.Number.Should().Be(51000);
            (await context.Database.GetAppliedMigrationsAsync()).Should().HaveCount(5);
            (await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM Bookings WHERE BookingCode = 'LEGACY'")
                .SingleAsync()).Should().Be(1);
        }
        finally { await isolated.DisposeAsync(); }
    }

    private static async Task<(Booking Booking, Trip OtherTrip, Seat Seat)> SeedJourney(ApplicationDbContext context)
    {
        var station = new Station { Code = Guid.NewGuid().ToString("N")[..10], Name = "Loop station" };
        var route = new Route
        {
            Code = Guid.NewGuid().ToString("N")[..10], Name = "Sightseeing loop", ServiceType = "Sightseeing",
            DepartureStation = station, ArrivalStation = station
        };
        var boat = new Boat { Code = Guid.NewGuid().ToString("N")[..10], Name = "Boat", TotalSeats = 1 };
        var seat = new Seat
        { Boat = boat, SeatCode = "A1", SeatClass = new SeatClass { Code = "STD", Name = "Standard" } };
        var trip = new Trip
        { Route = route, Boat = boat, DepartureTime = DateTimeOffset.UtcNow.AddDays(1), ArrivalTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(1) };
        var otherTrip = new Trip
        { Route = route, Boat = boat, DepartureTime = trip.DepartureTime.AddDays(1), ArrivalTime = trip.ArrivalTime.AddDays(1) };
        var routeStop = new RouteStop { Route = route, Station = station, SequenceNo = 1 };
        var from = new TripStopCall { Trip = trip, RouteStop = routeStop, VisitOrder = 1 };
        var to = new TripStopCall { Trip = trip, RouteStop = routeStop, VisitOrder = 3 };
        context.AddRange(seat, trip, otherTrip, from, to);
        await context.SaveChangesAsync();
        var order = new PurchaseOrder { PurchaserName = "Purchaser", PurchaserEmail = "p@example.com" };
        var booking = new Booking
        { BookingCode = Guid.NewGuid().ToString("N"), Order = order, OrderId = order.Id, CustomerName = "P", CustomerEmail = "p@example.com" };
        booking.SetJourney(from, to);
        return (booking, otherTrip, seat);
    }
}
