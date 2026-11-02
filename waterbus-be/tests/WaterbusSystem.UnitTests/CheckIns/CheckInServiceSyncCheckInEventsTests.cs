using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
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

        return new CheckInService(context, user.Object, clock.Object, new CheckInProcessor(context),
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
}
