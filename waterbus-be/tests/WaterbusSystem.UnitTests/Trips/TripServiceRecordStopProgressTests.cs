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
        _context.SaveChanges();
    }

    private TripService Service() => new(new TripRepository(_context), new IncidentRepository(_context), new EfUnitOfWork(_context),
        _user.Object, _clock.Object, _publisher.Object, Mock.Of<IUserDirectory>());

    private void AsAdmin() { _user.Setup(u => u.UserId).Returns(_adminId.ToString()); _user.Setup(u => u.UserRole).Returns("Admin"); }
    private void AsCaptain(Guid? id = null) { _user.Setup(u => u.UserId).Returns((id ?? _captainId).ToString()); _user.Setup(u => u.UserRole).Returns("Captain"); }

    private TripStopCall MiddleCall() => _context.TripStopCalls.Single(c => c.TripId == _seed.Trip.Id && c.VisitOrder == 2);

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
