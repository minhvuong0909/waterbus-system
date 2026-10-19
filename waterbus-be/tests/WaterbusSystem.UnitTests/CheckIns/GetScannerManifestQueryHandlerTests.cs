using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.CheckIns.Queries.GetScannerManifest;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using static WaterbusSystem.UnitTests.TestHelpers.CheckInScenarioFactory;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test GetScannerManifestQueryHandler đối chiếu BR-OFF-01 (preload manifest) và BR-BOARD-03
/// (chỉ Ticket đúng bến lên, chỉ cho Staff được phân công).
/// </summary>
public class GetScannerManifestQueryHandlerTests
{
    private readonly Guid _staffId = Guid.NewGuid();

    private GetScannerManifestQueryHandler CreateHandler(ApplicationDbContext context, DateTimeOffset now, string? role = "Staff")
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(_staffId.ToString());
        user.Setup(x => x.UserRole).Returns(role);

        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);

        return new GetScannerManifestQueryHandler(context, user.Object, clock.Object);
    }

    [Fact]
    public async Task Handle_WhenAssigned_ShouldReturnBookingWithPublicBookingIdAndTicket()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (seed, booking, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var handler = CreateHandler(context, now);
        var result = await handler.Handle(new GetScannerManifestQuery(boardingCall.Id), CancellationToken.None);

        result.TripStopCallId.Should().Be(boardingCall.Id);
        result.CheckInOpenAt.Should().Be(boardingCall.CheckInOpenAt);
        result.Trip.TripId.Should().Be(seed.Trip.Id);
        result.Trip.RouteName.Should().Be(seed.Route.Name);

        result.Bookings.Should().ContainSingle();
        var bookingDto = result.Bookings[0];
        bookingDto.BookingId.Should().Be(booking.Id);
        bookingDto.PublicBookingId.Should().Be(booking.PublicBookingId);
        bookingDto.Tickets.Should().ContainSingle(t => t.TicketId == ticket.Id);
    }

    [Fact]
    public async Task Handle_WhenStaffNotAssignedToStop_ShouldThrowForbiddenAccessException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, _, boardingCall, now) = SeedCheckInScenario(context, _staffId, withAssignment: false);

        var handler = CreateHandler(context, now);
        var act = () => handler.Handle(new GetScannerManifestQuery(boardingCall.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_WhenTicketBoardsAtAnotherStop_ShouldNotIncludeIt()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (seed, _, _, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var otherStop = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 2);
        var device = new ScannerDevice { DeviceCode = "DEV-OTHER", IsActive = true };
        context.ScannerDevices.Add(device);
        context.ScannerAssignments.Add(new ScannerAssignment
        {
            DeviceId = device.Id, Device = device, StaffAccountId = _staffId,
            TripStopCallId = otherStop.Id, TripStopCall = otherStop, IsPrimaryOffline = true,
            ActiveFrom = now.AddHours(-1), ActiveUntil = now.AddHours(1)
        });
        context.SaveChanges();

        var handler = CreateHandler(context, now);
        var result = await handler.Handle(new GetScannerManifestQuery(otherStop.Id), CancellationToken.None);

        result.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotStaff_ShouldThrowForbiddenAccessException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, _, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var handler = CreateHandler(context, now, role: "Admin");
        var act = () => handler.Handle(new GetScannerManifestQuery(boardingCall.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
