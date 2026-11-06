using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Scanner.Commands.ClaimPrimaryScanner;
using WaterbusSystem.Application.Features.Scanner.Commands.SyncOfflineCheckIns;
using WaterbusSystem.Application.Features.Scanner.Queries.GetScannerManifest;
using WaterbusSystem.Application.Features.Scanner.Queries.GetScannerPublicKey;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Scanner;

public class ScannerApiHandlerTests
{
    [Fact]
    public async Task GetScannerManifest_ReturnsOnlyAssignedStationTripsAndTickets()
    {
        using var context = CreateContext(out var fixture);
        var currentUser = CreateCurrentUser(fixture.StaffId);
        var signer = new Mock<IBookingQrSigner>();
        signer.Setup(x => x.GetPublicKey())
            .Returns(new BookingQrPublicKey("test-key", "RS256", "test-pem"));

        var handler = new GetScannerManifestQueryHandler(context, currentUser.Object, signer.Object);
        var result = await handler.Handle(new GetScannerManifestQuery(fixture.Station.Code), CancellationToken.None);

        result.Trips.Should().ContainSingle();
        result.Bookings.Should().ContainSingle();
        result.Bookings[0].Tickets.Should().ContainSingle();
        result.PublicKey.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetScannerPublicKey_ReturnsCurrentSigningKey()
    {
        var signer = new Mock<IBookingQrSigner>();
        signer.Setup(x => x.GetPublicKey())
            .Returns(new BookingQrPublicKey("test-key", "RS256", "test-pem"));

        var result = await new GetScannerPublicKeyQueryHandler(signer.Object)
            .Handle(new GetScannerPublicKeyQuery(), CancellationToken.None);

        result.Kid.Should().Be("test-key");
        result.Algorithm.Should().Be("RS256");
        result.PublicKeyPem.Should().Be("test-pem");
    }

    [Fact]
    public async Task ClaimPrimaryScanner_SetsPrimaryForExistingAssignment()
    {
        using var context = CreateContext(out var fixture, primary: false);
        var handler = new ClaimPrimaryScannerCommandHandler(context, CreateCurrentUser(fixture.StaffId).Object);

        var result = await handler.Handle(
            new ClaimPrimaryScannerCommand(fixture.Device.DeviceCode, fixture.BoardingCall.Id, null, null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.IsPrimaryOffline.Should().BeTrue();
        context.ScannerAssignments.Single().IsPrimaryOffline.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimPrimaryScanner_RejectsWhenAnotherDeviceIsPrimary()
    {
        using var context = CreateContext(out var fixture, primary: false);
        var otherDevice = new ScannerDevice { DeviceCode = "DEV-OTHER" };
        context.ScannerDevices.Add(otherDevice);
        context.ScannerAssignments.Add(new ScannerAssignment
        {
            DeviceId = otherDevice.Id,
            Device = otherDevice,
            StaffAccountId = Guid.NewGuid(),
            TripStopCallId = fixture.BoardingCall.Id,
            IsPrimaryOffline = true,
            ActiveFrom = DateTimeOffset.UtcNow.AddHours(-1),
            ActiveUntil = DateTimeOffset.UtcNow.AddHours(1)
        });
        await context.SaveChangesAsync();

        var handler = new ClaimPrimaryScannerCommandHandler(context, CreateCurrentUser(fixture.StaffId).Object);

        var act = () => handler.Handle(
            new ClaimPrimaryScannerCommand(fixture.Device.DeviceCode, fixture.BoardingCall.Id, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task SyncOfflineCheckIns_UpdatesTicketAndDeduplicatesClientEvent()
    {
        using var context = CreateContext(out var fixture);
        var handler = new SyncOfflineCheckInsCommandHandler(context, CreateCurrentUser(fixture.StaffId).Object);
        var command = CreateSyncCommand(fixture, DateTimeOffset.UtcNow);

        var firstResult = await handler.Handle(command, CancellationToken.None);
        var duplicateResult = await handler.Handle(command, CancellationToken.None);

        firstResult.SuccessCount.Should().Be(1);
        duplicateResult.SuccessCount.Should().Be(1);
        context.CheckInEvents.Should().ContainSingle();
        context.Tickets.Single().BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
        context.Tickets.Single().CheckedInByStaffId.Should().Be(fixture.StaffId);
    }

    [Fact]
    public async Task SyncOfflineCheckIns_RejectsEventOutsideConfiguredWindow()
    {
        using var context = CreateContext(out var fixture);
        var handler = new SyncOfflineCheckInsCommandHandler(context, CreateCurrentUser(fixture.StaffId).Object);

        var result = await handler.Handle(
            CreateSyncCommand(fixture, DateTimeOffset.UtcNow.AddHours(2)),
            CancellationToken.None);

        result.SuccessCount.Should().Be(0);
        result.ConflictCount.Should().Be(1);
        result.ProcessedEvents[0].Outcome.Should().Be("Conflict");
        context.Tickets.Single().BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    private static ApplicationDbContext CreateContext(out ScannerFixture fixture, bool primary = true)
    {
        var context = TestDbContextFactory.CreateDbContext();
        var tripSeed = TestDbContextFactory.SeedTrip(context);
        var now = DateTimeOffset.UtcNow;
        var boardingCall = tripSeed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var disembarkingCall = tripSeed.Trip.TripStopCalls.Single(c => c.VisitOrder == 3);
        boardingCall.CheckInOpenAt = now.AddHours(-1);
        boardingCall.CheckInCloseAt = now.AddHours(1);
        context.TripStopCalls.Update(boardingCall);

        var staffId = Guid.NewGuid();
        var device = new ScannerDevice { DeviceCode = "DEV-STAFF-01" };
        context.ScannerDevices.Add(device);
        context.ScannerAssignments.Add(new ScannerAssignment
        {
            Device = device,
            DeviceId = device.Id,
            StaffAccountId = staffId,
            TripStopCallId = boardingCall.Id,
            IsPrimaryOffline = primary,
            ActiveFrom = now.AddHours(-1),
            ActiveUntil = now.AddHours(1)
        });

        var order = new PurchaseOrder
        {
            PurchaserName = "Test Passenger",
            PurchaserEmail = "test@example.com",
            Status = OrderStatus.Confirmed,
            QuotedTotal = 15000m
        };
        var booking = new Booking
        {
            Order = order,
            OrderId = order.Id,
            TripId = tripSeed.Trip.Id,
            BoardingCallId = boardingCall.Id,
            DisembarkingCallId = disembarkingCall.Id,
            BookingCode = "WB-TEST-01",
            PublicBookingId = "public-test-booking",
            CustomerName = "Test Passenger",
            Status = BookingStatus.Confirmed
        };
        context.PurchaseOrders.Add(order);
        context.Bookings.Add(booking);

        var reservation = new SeatReservation
        {
            TripId = tripSeed.Trip.Id,
            SeatId = tripSeed.Seat1.Id,
            BookingId = booking.Id,
            BoardingStopOrder = 1,
            DisembarkingStopOrder = 3,
            Status = ReservationStatus.Confirmed,
            PassengerName = "Test Passenger",
            SeatCodeAtSale = "S01",
            SeatClassAtSale = "Standard"
        };
        context.SeatReservations.Add(reservation);
        var ticket = new Ticket
        {
            BookingId = booking.Id,
            SeatReservationId = reservation.Id,
            TicketCode = "TK-TEST-01",
            Status = TicketStatus.Valid
        };
        context.Tickets.Add(ticket);
        context.SaveChanges();

        fixture = new ScannerFixture(
            tripSeed.StationA,
            boardingCall,
            staffId,
            device,
            ticket,
            tripSeed.Trip.Id);
        return context;
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid staffId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(staffId.ToString());
        return currentUser;
    }

    private static SyncOfflineCheckInsCommand CreateSyncCommand(
        ScannerFixture fixture,
        DateTimeOffset occurredAt) =>
        new(
            fixture.Device.DeviceCode,
            occurredAt,
            [
                new OfflineCheckInEventDto(
                    "event-test-01",
                    fixture.Ticket.Id,
                    fixture.TripId,
                    fixture.BoardingCall.Id,
                    occurredAt,
                    null)
            ],
            "BATCH-TEST-01");

    private sealed record ScannerFixture(
        Station Station,
        TripStopCall BoardingCall,
        Guid StaffId,
        ScannerDevice Device,
        Ticket Ticket,
        Guid TripId);
}
