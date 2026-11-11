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
            new SqlScannerAssignmentLock(context),
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
    public async Task SyncCheckInEventsAsync_WhenSameClientEventIdReusedWithDifferentTicket_ShouldReportMismatchNotIdempotentReplay()
    {
        // CHK-06: ClientEventId chỉ là khoá idempotency hợp lệ cho ĐÚNG request gốc. Gửi lại cùng
        // key nhưng đổi TicketId phải bị báo xung đột, không được âm thầm trả kết quả check-in cũ
        // (có thể lộ thông tin vé khác cho Staff không liên quan).
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, now) = SeedCheckInScenario(context, _staffId);

        var service = CreateService(context, now);
        var firstRequest = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-reused-key", now, null)
        });
        await service.SyncCheckInEventsAsync(firstRequest, CancellationToken.None);

        // Dùng lại đúng ClientEventId của lần gửi trên nhưng đổi TicketId - mô phỏng key bị tái sử
        // dụng sai (bug client hoặc dò đoán key), không cần vé thứ 2 có thật để kiểm tra nhánh đối
        // chiếu identity này (nó so khớp trước khi tra vé).
        var mismatchedTicketId = Guid.NewGuid();
        var reusedKeyRequest = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(mismatchedTicketId, boardingCall.Id, "evt-reused-key", now, null)
        });

        var result = await service.SyncCheckInEventsAsync(reusedKeyRequest, CancellationToken.None);

        var single = result.Results.Single();
        single.Outcome.Should().Be("ClientEventIdMismatch");
        single.WasAlreadySynced.Should().BeFalse();
        // Vé gốc vẫn giữ đúng kết quả check-in thành công của request đầu tiên.
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.CheckedIn);
        context.CheckInEvents.Count(e => e.ClientEventId == "evt-reused-key").Should().Be(1);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenScanWasValidButSyncHappensLater_ShouldStampCheckedInAtWithDeviceTimeNotServerTime()
    {
        // CHK-05: CheckedInAt phải phản ánh thời điểm Staff thực sự quét vé (occurredAtDevice),
        // không phải thời điểm server nhận lô đồng bộ offline muộn hơn.
        using var context = TestDbContextFactory.CreateDbContext();
        var (seed, _, ticket, boardingCall, occurredAtDevice) = SeedCheckInScenario(context, _staffId);

        var syncedAtServer = seed.Trip.DepartureTime.AddMinutes(30); // sync muộn hơn nhiều so với lúc quét thật
        var service = CreateService(context, syncedAtServer);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-device-time", occurredAtDevice, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Single().Outcome.Should().Be("Success");
        var updatedTicket = context.Tickets.Single(t => t.Id == ticket.Id);
        updatedTicket.CheckedInAt.Should().Be(occurredAtDevice, "CheckedInAt phải là giờ quét thật trên máy, không phải giờ server nhận lô sync");
        updatedTicket.CheckedInAt.Should().NotBe(syncedAtServer);
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
        // Đối chứng: occurredAtDevice thật sự ngoài cửa sổ (nhưng không ở tương lai so với server, và vẫn
        // trong hạn ScannerAssignment) vẫn phải bị từ chối, dù server nhận lúc đang trong cửa sổ.
        using var context = TestDbContextFactory.CreateDbContext();
        var (seed, _, ticket, boardingCall, serverNow) = SeedCheckInScenario(context, _staffId);

        var scannedBeforeWindowOpened = seed.Trip.DepartureTime.AddMinutes(-45);
        var service = CreateService(context, serverNow);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-really-late-scan", scannedBeforeWindowOpened, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-really-late-scan" && r.Outcome == "OutOfWindow");
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenDeviceTimeIsInTheFuture_ShouldReportInvalidDeviceTimeWithoutCheckingTicket()
    {
        // BR-OFF-04/05: occurredAtDevice do client tự khai báo - không được tin vô điều kiện. Một thời điểm
        // quét ở TƯƠNG LAI so với lúc server nhận chắc chắn là dữ liệu thiết bị sai (đồng hồ lệch hoặc giả).
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, serverNow) = SeedCheckInScenario(context, _staffId);

        var futureDeviceTime = serverNow.AddHours(1);
        var service = CreateService(context, serverNow);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-future-device-time", futureDeviceTime, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-future-device-time" && r.Outcome == "InvalidDeviceTime");
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenAssignmentExpiredByServerTimeButNotByDeviceTime_ShouldStillSucceed()
    {
        // Điểm phân công (ScannerAssignment.ActiveUntil) phải đối chiếu với occurredAtDevice khi offline,
        // không phải now - nếu không, phân công hết hạn đúng lúc lô đồng bộ muộn tới sẽ biến 1 quét hợp lệ
        // trong hạn phân công thành "NotAssigned" sai (review PR #56, điểm 3).
        using var context = TestDbContextFactory.CreateDbContext();
        var (seed, _, ticket, boardingCall, occurredAtDevice) = SeedCheckInScenario(context, _staffId);
        // Seed: ScannerAssignment.ActiveUntil = departure + 1h; occurredAtDevice (= Now trả về) = departure - 10m,
        // nằm trong hạn phân công và trong cửa sổ check-in.

        var syncedAfterAssignmentExpired = seed.Trip.DepartureTime.AddHours(2); // Sau ActiveUntil rất lâu.
        var service = CreateService(context, syncedAfterAssignmentExpired);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-assignment-expired-by-sync-time", occurredAtDevice, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-assignment-expired-by-sync-time" && r.Outcome == "Success");
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenDeviceTimeIsFalselyBackdatedIntoAPreviouslyValidWindow_ShouldReportInvalidDeviceTime()
    {
        // Review PR #56 (2nd round), điểm 1: occurredAtDevice "trông hợp lệ" (đúng trong cửa sổ check-in và
        // hạn ScannerAssignment của MỘT CHUYẾN TRƯỚC) không được tự động tin nếu khoảng cách tới lúc server
        // nhận (now) vượt quá MaxOfflineSkewHours - dấu hiệu rõ của giả mạo/đồng hồ thiết bị sai, không phải
        // 1 lần mất kết nối hợp lý (BR-OFF-04/05).
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, occurredAtDevice) = SeedCheckInScenario(context, _staffId);
        // occurredAtDevice (= Now trả về từ seed) nằm trong cửa sổ check-in VÀ trong hạn ScannerAssignment.

        var syncedFarBeyondSkewThreshold = occurredAtDevice.AddHours(new OfflineCheckInReconciliationOptions().MaxOfflineSkewHours + 1);
        var service = CreateService(context, syncedFarBeyondSkewThreshold);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-falsely-backdated", occurredAtDevice, null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-falsely-backdated" && r.Outcome == "InvalidDeviceTime");
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
    }

    [Fact]
    public async Task SyncCheckInEventsAsync_WhenTicketAlreadyFinalizedAsNoShow_ShouldNotRevertToCheckedIn()
    {
        // Review PR #56 (2nd round), điểm 2: RunNoShowFinalizationAsync đã kết luận NoShow; 1 lô offline
        // đến muộn hơn nữa (vd scanner mất sóng cả buổi) không được âm thầm đảo lại thành CheckedIn mà
        // không qua đối soát rõ ràng (BR-OFF-04).
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, _, ticket, boardingCall, _) = SeedCheckInScenario(context, _staffId);

        var finalizedAt = boardingCall.CheckInCloseAt!.Value.AddMinutes(16); // Qua grace 15p mặc định.
        var finalizeService = CreateService(context, finalizedAt);
        await finalizeService.RunNoShowFinalizationAsync(CancellationToken.None);
        context.Tickets.Single(t => t.Id == ticket.Id).BoardingStatus.Should().Be(BoardingStatus.NoShow); // Tiền điều kiện.

        // Lô offline đến muộn hơn NỮA, mang 1 event có occurredAtDevice trông "hợp lệ" (trong cửa sổ check-in).
        var syncedMuchLater = finalizedAt.AddHours(1);
        var service = CreateService(context, syncedMuchLater);
        var request = new SyncCheckInEventsRequest(new[]
        {
            new CheckInEventInput(ticket.Id, boardingCall.Id, "evt-after-noshow", boardingCall.CheckInOpenAt!.Value.AddMinutes(5), null)
        });

        var result = await service.SyncCheckInEventsAsync(request, CancellationToken.None);

        result.Results.Should().ContainSingle(r => r.ClientEventId == "evt-after-noshow" && r.Outcome == "NoShowFinalized");
        var updated = context.Tickets.Single(t => t.Id == ticket.Id);
        updated.BoardingStatus.Should().Be(BoardingStatus.NoShow);
        updated.NoShowFinalizedAt.Should().Be(finalizedAt);
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
            new CheckInProcessor(repository), new SqlScannerAssignmentLock(context),
            Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);

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
