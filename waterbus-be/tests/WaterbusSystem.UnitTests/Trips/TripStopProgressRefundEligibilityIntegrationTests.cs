using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Application.Features.Trips.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

/// <summary>
/// Review PR #56 điểm 4: B01 không được coi là xong end-to-end chỉ vì ghi tiến độ và đánh giá hoàn tiền mỗi
/// phần đều có unit test riêng - test này nối TripService.ChangeStatusAsync/RecordStopProgressAsync (ghi
/// dữ liệu TripStopCall THẬT qua DbContext) với RefundEligibilityEvaluator (đọc lại đúng dữ liệu đó) cho
/// đúng ví dụ A→B→C→D bị Terminate tại C của BR-RF-02/03 (Ví dụ B trong BR v2.0).
/// </summary>
public class TripStopProgressRefundEligibilityIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly Guid _captainId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public TripStopProgressRefundEligibilityIntegrationTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context); // Bến A(1), B(2), C(3)
        _seed.Boat.CaptainUserId = _captainId;

        var stationD = new Station { Code = "ST04", Name = "Ben D", Address = "D", OrderIndex = 4, IsActive = true };
        var routeStop = new RouteStop { RouteId = _seed.Route.Id, StationId = stationD.Id, SequenceNo = 4 };
        _context.Stations.Add(stationD);
        _context.RouteStops.Add(routeStop);
        _context.TripStopCalls.Add(new TripStopCall { TripId = _seed.Trip.Id, RouteStop = routeStop, VisitOrder = 4, Status = "Planned" });
        _context.SaveChanges();
    }

    private TripService Service() => new(new TripRepository(_context), new IncidentRepository(_context), new EfUnitOfWork(_context),
        _user.Object, _clock.Object, Mock.Of<IOperationsAlertPublisher>(), Mock.Of<IUserDirectory>(),
        TestNotifications.Outbox(_context), new DirectCommercialTransactionRunner());

    private void AsCaptain() { _user.Setup(u => u.UserId).Returns(_captainId.ToString()); _user.Setup(u => u.UserRole).Returns("Captain"); }
    private void AsAdmin() { _user.Setup(u => u.UserId).Returns(_adminId.ToString()); _user.Setup(u => u.UserRole).Returns("Admin"); }

    private TripStopCall CallAt(int visitOrder) => _context.TripStopCalls.Single(c => c.TripId == _seed.Trip.Id && c.VisitOrder == visitOrder);

    private static Booking MakeBooking(TripStopCall boarding, TripStopCall disembarking)
    {
        var booking = new Booking { Status = BookingStatus.Confirmed, QuotedTotal = 15000m };
        booking.SetJourney(boarding, disembarking);
        return booking;
    }

    private static Ticket MakeTicket(BoardingStatus boarding = BoardingStatus.CheckedIn) =>
        new() { Status = TicketStatus.Valid, BoardingStatus = boarding, PaidFareSnapshot = 15000m, FaceFareSnapshot = 15000m };

    [Fact]
    public async Task TripTerminatedAtStationC_ShouldMatchBusinessRuleExampleB_UsingRealPersistedStopProgress()
    {
        AsCaptain();
        var service = Service();

        // A→EnRoute: Start tự ghi bến A Departed (TripService.ChangeStatusAsync).
        await service.ChangeStatusAsync(_seed.Trip.Id, new ChangeTripStatusRequest(TripOperation.Start, null, null), CancellationToken.None);

        // Bến B: tới rồi rời - đã phục vụ xong qua endpoint RecordStopProgress mới.
        var callB = CallAt(2);
        await service.RecordStopProgressAsync(_seed.Trip.Id, callB.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);
        await service.RecordStopProgressAsync(_seed.Trip.Id, callB.Id, new RecordStopProgressRequest(StopCallProgress.Departed), CancellationToken.None);

        // Bến C: chỉ mới TỚI (chưa rời) khi sự cố buộc Terminate - đúng kịch bản "chấm dứt giữa đường".
        var callC = CallAt(3);
        await service.RecordStopProgressAsync(_seed.Trip.Id, callC.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        AsAdmin(); // Terminate là quyết định của Admin, không phải Captain (BR-OPS-04).
        await service.ChangeStatusAsync(_seed.Trip.Id,
            new ChangeTripStatusRequest(TripOperation.Terminate, "Sự cố máy tại bến C", null), CancellationToken.None);

        var trip = _context.Trips.Single(t => t.Id == _seed.Trip.Id);
        var callA = CallAt(1);
        var callD = CallAt(4);

        // B101: A -> B, đã phục vụ xong trước khi Terminate -> KHÔNG hoàn.
        RefundEligibilityEvaluator.Evaluate(MakeTicket(), MakeBooking(callA, callB), trip)
            .IsEligible.Should().BeFalse();

        // B102: B -> D, chưa tới đích (chưa qua C lẫn D) -> hoàn 100%.
        RefundEligibilityEvaluator.Evaluate(MakeTicket(), MakeBooking(callB, callD), trip)
            .IsEligible.Should().BeTrue();

        // B103: C -> D, mới chỉ TỚI C (chưa rời), chưa tới D -> hoàn 100%.
        RefundEligibilityEvaluator.Evaluate(MakeTicket(boarding: BoardingStatus.NotCheckedIn), MakeBooking(callC, callD), trip)
            .IsEligible.Should().BeTrue();
    }
}
