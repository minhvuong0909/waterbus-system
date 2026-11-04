using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using static WaterbusSystem.UnitTests.TestHelpers.CheckInScenarioFactory;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test CheckInService.SyncCheckInEventsAsync đối chiếu BR-OFF-03 (idempotent sync theo lô) và đảm bảo
/// 1 event lỗi trong lô không làm mất kết quả của các event khác đã xử lý thành công.
/// </summary>
public class CheckInServiceSyncCheckInEventsTests
{
    private readonly Guid _staffId = Guid.NewGuid();

    private CheckInService CreateService(ApplicationDbContext context, DateTimeOffset now, string? role = "Staff")
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(_staffId.ToString());
        user.Setup(x => x.UserRole).Returns(role);

        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(now);

        return new CheckInService(new CheckInRepository(context), new EfUnitOfWork(context),
            user.Object, clock.Object, new CheckInProcessor(new CheckInRepository(context)),
            Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenBatchHasOneValidEvent_ShouldCheckInSuccessfully()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var service = CreateService(context, now);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-1", now, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-1" && r.Outcome == "Success" && !r.WasAlreadySynced);
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenOneEventIsInvalidAndAnotherIsValid_ShouldNotLoseTheValidOne()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var service = CreateService(context, now);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(Guid.NewGuid(), boardingCall.Id, "evt-missing-ticket", now, null), // Ticket không tồn tại
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-valid", now, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-missing-ticket" && r.Outcome == "Error");
        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-valid" && r.Outcome == "Success");
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenSameBatchSyncedTwice_ShouldBeIdempotent()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var service = CreateService(context, now);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-dup", now, null)
        });

        await service.SyncCheckInEventsAsync(request, CancellationToken.None);
        // Gửi lại đúng lô cũ (do mất mạng giữa chừng, client không chắc server đã nhận hay chưa).
        var second = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        second.Results.Single().WasAlreadySynced.Should().BeTrue();
        context.CheckInEvents.Count(e => e.ClientEventId == "evt-dup").Should().Be(1);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenScanWasValidButSyncHappensAfterWindowCloses_ShouldStillSucceed()
    {
        // BR-OFF-05: cửa sổ check-in phải đối chiếu với thời điểm quét THẬT trên máy (occurredAtDevice),
        // không phải giờ server nhận lúc đồng bộ - mạng khôi phục muộn không được biến 1 quét hợp lệ thành lỗi.
        using var context = TestDbContextFactory.CreateDbContext();
        var (seed, _, ticket, boardingCall, occurredAtDevice) = SeedCheckInScenario(context, _staffId);

        // Sau CheckInCloseAt (departure-5m) nhưng vẫn trong hạn ScannerAssignment (đến departure+1h)
        // - đúng tình huống thực tế: mạng khôi phục muộn, thiết bị vẫn còn được phân công.
        var syncedMuchLater = seed.Trip.DepartureTime.AddMinutes(30);
        var service = CreateService(context, syncedMuchLater);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-late-sync", occurredAtDevice, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-late-sync" && r.Outcome == "Success");
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenDeviceScanTimeItselfIsOutsideWindow_ShouldStillReportOutOfWindow()
    {
        // Đối chứng: occurredAtDevice thật sự ngoài cửa sổ vẫn phải bị từ chối, dù server nhận lúc đang trong cửa sổ.
        using var context = TestDbContextFactory.CreateDbContext();
        var (seed, _, ticket, boardingCall, serverNow) = SeedCheckInScenario(context, _staffId);

        var scannedAfterClose = seed.Trip.DepartureTime.AddHours(1);
        var service = CreateService(context, serverNow);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-really-late-scan", scannedAfterClose, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-really-late-scan" && r.Outcome == "OutOfWindow");
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenCallerIsNotStaff_ShouldThrowForbiddenAccessException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var service = CreateService(context, now, role: "Captain");
        var act = () => service.SyncCheckInEventsAsync(
            new SyncCheckInEventsRequest(new[] { new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-x", now, null) }),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenSaveThrowsConcurrencyException_ShouldReportErrorAndKeepProcessingBatch()
    {
        // IUnitOfWork.SaveChangesAsync là nơi duy nhất dịch DbUpdateConcurrencyException sang ConcurrencyException
        // (xem EfUnitOfWork) - giả lập trực tiếp ở đây để test không phụ thuộc việc EF Core InMemory provider
        // có tự sinh giá trị RowVersion mới khi ghi hay không (không đáng tin cậy để mô phỏng xung đột thật).
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("Vé vừa được cập nhật bởi người khác."));

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(_staffId.ToString());
        user.Setup(u => u.UserRole).Returns("Staff");
        var clock = new Mock<IDateTimeService>();
        clock.Setup(c => c.Now).Returns(now);

        var repository = new CheckInRepository(context);
        var service = new CheckInService(repository, unitOfWork.Object, user.Object, clock.Object,
            new CheckInProcessor(repository), Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);

        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-conflict", now, null)
        });

        // Không ném ra ngoài: lỗi lưu của 1 event trong lô phải được nuốt lại thành 1 kết quả "Error" (BR-OFF-03),
        // không làm fail cả SyncCheckInEventsAsync.
        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r =>
            r.ClientEventId == "evt-conflict" && r.Outcome == "Error" && !r.WasAlreadySynced && r.Error != null);
        unitOfWork.Verify(u => u.DiscardTrackedChanges(), Times.Once);
    }
}
