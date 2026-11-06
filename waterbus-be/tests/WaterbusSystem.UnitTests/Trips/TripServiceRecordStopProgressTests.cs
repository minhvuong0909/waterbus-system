using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Features.Trips.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Trips;

/// <summary>
/// Test TripService.RecordStopProgressAsync (BR-STOP-02): ghi nhận tiến độ phục vụ tại 1 điểm dừng
/// trung gian mà không đổi Trip.Status (BR-OPS-02) - dữ liệu mà RefundEligibilityEvaluator cần để
/// đánh giá đúng đoạn đi đã thực sự được phục vụ khi Trip bị Terminate giữa đường (BR-RF-02/03).
/// </summary>
public class TripServiceRecordStopProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly Mock<IOperationsAlertPublisher> _publisher = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _captainId = Guid.NewGuid();

    public TripServiceRecordStopProgressTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
        _seed.Boat.CaptainUserId = _captainId;
        _seed.Trip.Status = TripStatus.EnRoute;
        _seed.Trip.ActualDepartureTime = Now.AddHours(-1);
        // Mô phỏng đúng việc Start đã chạy (ChangeStatusAsync tự ghi điểm dừng đầu Departed - xem
        // TripServiceChangeStatusTests.StartThenArrive_...) để các test ở đây không bị chặn bởi quy tắc
        // thứ tự VisitOrder (điểm dừng trước phải đã Departed) khi test riêng điểm dừng trung gian.
        var firstCall = _context.TripStopCalls.Single(c => c.TripId == _seed.Trip.Id && c.VisitOrder == 1);
        firstCall.Status = "Departed";
        firstCall.ActualArrivalTime = Now.AddHours(-1);
        firstCall.ActualDepartureTime = Now.AddHours(-1);
        _context.SaveChanges();
    }

    private TripService Service() => new(new TripRepository(_context), new IncidentRepository(_context), new EfUnitOfWork(_context),
        _user.Object, _clock.Object, _publisher.Object, Mock.Of<IUserDirectory>(),
        TestNotifications.Outbox(_context), new DirectCommercialTransactionRunner());

    private void AsAdmin() { _user.Setup(u => u.UserId).Returns(_adminId.ToString()); _user.Setup(u => u.UserRole).Returns("Admin"); }
    private void AsCaptain(Guid? id = null) { _user.Setup(u => u.UserId).Returns((id ?? _captainId).ToString()); _user.Setup(u => u.UserRole).Returns("Captain"); }

    private TripStopCall MiddleCall() => _context.TripStopCalls.Single(c => c.TripId == _seed.Trip.Id && c.VisitOrder == 2);
    private TripStopCall FirstCall() => _context.TripStopCalls.Single(c => c.TripId == _seed.Trip.Id && c.VisitOrder == 1);
    private TripStopCall LastCall() => _context.TripStopCalls.Single(c => c.TripId == _seed.Trip.Id && c.VisitOrder == 3);

    /// <summary>Thêm bến D (VisitOrder 4) để có 2 điểm dừng trung gian (B, C) kiểm tra thứ tự vượt trước.</summary>
    private TripStopCall AddFourthStop()
    {
        var stationD = new Station { Code = "ST04", Name = "Ben D", Address = "D", OrderIndex = 4, IsActive = true };
        var routeStop = new RouteStop { RouteId = _seed.Route.Id, StationId = stationD.Id, SequenceNo = 4 };
        var call = new TripStopCall { TripId = _seed.Trip.Id, RouteStop = routeStop, VisitOrder = 4, Status = "Planned" };
        _context.Stations.Add(stationD);
        _context.RouteStops.Add(routeStop);
        _context.TripStopCalls.Add(call);
        _context.SaveChanges();
        return call;
    }

    [Fact]
    public async Task Captain_RecordsArrivedAtIntermediateStop_ShouldStampArrivalWithoutChangingTripStatus()
    {
        AsCaptain();
        var call = MiddleCall();

        var result = await Service().RecordStopProgressAsync(
            _seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        result.Status.Should().Be("Arrived");
        result.ActualArrivalTime.Should().Be(Now);
        result.ActualDepartureTime.Should().BeNull();
        _context.Trips.Find(_seed.Trip.Id)!.Status.Should().Be(TripStatus.EnRoute);
    }

    [Fact]
    public async Task Admin_RecordsDepartedWithoutPriorArrived_ShouldStampBothArrivalAndDeparture()
    {
        AsAdmin();
        var call = MiddleCall();

        var result = await Service().RecordStopProgressAsync(
            _seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Departed), CancellationToken.None);

        result.Status.Should().Be("Departed");
        result.ActualArrivalTime.Should().Be(Now);
        result.ActualDepartureTime.Should().Be(Now);
    }

    [Fact]
    public async Task RecordArrivedThenDeparted_ShouldKeepOriginalArrivalTime()
    {
        AsAdmin();
        var call = MiddleCall();
        var service = Service();
        await service.RecordStopProgressAsync(_seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        var later = Now.AddMinutes(5);
        _clock.Setup(c => c.Now).Returns(later);
        var result = await service.RecordStopProgressAsync(_seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Departed), CancellationToken.None);

        result.ActualArrivalTime.Should().Be(Now);
        result.ActualDepartureTime.Should().Be(later);
    }

    [Fact]
    public async Task CaptainOfAnotherBoat_ShouldBeForbidden()
    {
        AsCaptain(Guid.NewGuid());
        var call = MiddleCall();

        var act = () => Service().RecordStopProgressAsync(
            _seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task PassengerRole_ShouldBeForbidden()
    {
        _user.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _user.Setup(u => u.UserRole).Returns("Passenger");
        var call = MiddleCall();

        var act = () => Service().RecordStopProgressAsync(
            _seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task TripNotEnRoute_ShouldThrowConflictException()
    {
        AsAdmin();
        _seed.Trip.Status = TripStatus.Scheduled;
        _context.SaveChanges();
        var call = MiddleCall();

        var act = () => Service().RecordStopProgressAsync(
            _seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AlreadyDeparted_RecordingArrivedAgain_ShouldThrowConflictException()
    {
        AsAdmin();
        var call = MiddleCall();
        var service = Service();
        await service.RecordStopProgressAsync(_seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Departed), CancellationToken.None);

        var act = () => service.RecordStopProgressAsync(_seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task SkippedStop_ShouldThrowConflictException()
    {
        AsAdmin();
        var call = MiddleCall();
        call.Status = "Skipped";
        _context.SaveChanges();

        var act = () => Service().RecordStopProgressAsync(
            _seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task RecordingAtFirstStop_ShouldBeRejected_BecauseItIsOnlyRecordedViaStart()
    {
        // Review PR #56 điểm 2: cho phép ghi đè điểm dừng ĐẦU ở đây là vô nghĩa (Start đã tự ghi) và không
        // được dùng để "sửa" lại dữ liệu của điểm dừng mà ChangeStatusAsync sở hữu.
        AsAdmin();
        var call = FirstCall();

        var act = () => Service().RecordStopProgressAsync(
            _seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Departed), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task RecordingAtLastStop_ShouldBeRejected_BecausePrematureMarkWouldWronglyDenyRefundAfterTermination()
    {
        // Review PR #56 điểm 2: nếu cho phép đánh dấu điểm dừng CUỐI "Arrived" ở đây khi Trip còn EnRoute,
        // RefundEligibilityEvaluator sẽ nghĩ hành trình đã hoàn tất dù Trip sau đó bị Terminate trước khi
        // thực sự tới bến cuối - từ chối hoàn tiền sai cho khách chưa được phục vụ (BR-RF-02/03).
        AsAdmin();
        var call = LastCall();

        var act = () => Service().RecordStopProgressAsync(
            _seed.Trip.Id, call.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _context.TripStopCalls.Find(call.Id)!.Status.Should().Be("Planned");
    }

    [Fact]
    public async Task RecordingLaterIntermediateStopBeforeEarlierOne_ShouldThrowConflictException()
    {
        // Review PR #56 điểm 2: không được ghi nhận vượt trước - bến C (VisitOrder 3, giờ là trung gian vì
        // đã thêm bến D) chỉ được ghi nhận sau khi bến B (VisitOrder 2) đã Departed.
        AsAdmin();
        AddFourthStop();
        var stationC = _context.TripStopCalls.Single(c => c.TripId == _seed.Trip.Id && c.VisitOrder == 3);

        var act = () => Service().RecordStopProgressAsync(
            _seed.Trip.Id, stationC.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task RecordingIntermediateStopsInOrder_ShouldSucceedForEachOnceThePreviousOneDeparted()
    {
        // Đối chứng: cùng 2 điểm dừng trung gian ở trên, nhưng đi ĐÚNG thứ tự (B rồi mới C) phải thành công.
        AsAdmin();
        AddFourthStop();
        var service = Service();
        var stationB = MiddleCall();
        var stationC = _context.TripStopCalls.Single(c => c.TripId == _seed.Trip.Id && c.VisitOrder == 3);

        await service.RecordStopProgressAsync(_seed.Trip.Id, stationB.Id, new RecordStopProgressRequest(StopCallProgress.Departed), CancellationToken.None);
        var result = await service.RecordStopProgressAsync(_seed.Trip.Id, stationC.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        result.Status.Should().Be("Arrived");
    }

    [Fact]
    public async Task StopCallFromAnotherTrip_ShouldThrowNotFoundException()
    {
        AsAdmin();
        var otherSeed = TestDbContextFactory.SeedTrip(_context);
        var foreignCall = _context.TripStopCalls.Single(c => c.TripId == otherSeed.Trip.Id && c.VisitOrder == 2);

        var act = () => Service().RecordStopProgressAsync(
            _seed.Trip.Id, foreignCall.Id, new RecordStopProgressRequest(StopCallProgress.Arrived), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
