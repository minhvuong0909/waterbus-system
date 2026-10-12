using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using WaterbusSystem.WebApi.Hubs;
using Xunit;

namespace WaterbusSystem.UnitTests.Realtime;

public class SeatSyncHubTests
{
    private readonly Mock<IGroupManager> _groups = new();
    private readonly SeatSyncHub _hub;

    public SeatSyncHubTests()
    {
        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns("conn-1");
        context.Setup(c => c.Items).Returns(new Dictionary<object, object?>());
        _hub = new SeatSyncHub { Context = context.Object, Groups = _groups.Object };
    }

    [Fact]
    public async Task JoinTrip_ShouldAddConnectionToTripGroup()
    {
        var tripId = Guid.NewGuid();

        await _hub.JoinTrip(tripId);

        _groups.Verify(g => g.AddToGroupAsync("conn-1", $"trip:{tripId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinTrip_EmptyId_ShouldThrow()
    {
        var act = () => _hub.JoinTrip(Guid.Empty);

        await act.Should().ThrowAsync<HubException>();
    }

    [Fact]
    public async Task JoinTrip_OverLimit_ShouldThrow_ButRejoiningSameTripIsAllowed()
    {
        var trips = Enumerable.Range(0, SeatSyncHub.MaxTripsPerConnection).Select(_ => Guid.NewGuid()).ToList();
        foreach (var trip in trips) await _hub.JoinTrip(trip);

        await _hub.JoinTrip(trips[0]); // join lại chuyến đã theo dõi không tính thêm
        var act = () => _hub.JoinTrip(Guid.NewGuid());

        await act.Should().ThrowAsync<HubException>();
    }

    [Fact]
    public async Task LeaveTrip_ShouldFreeSlot()
    {
        var trips = Enumerable.Range(0, SeatSyncHub.MaxTripsPerConnection).Select(_ => Guid.NewGuid()).ToList();
        foreach (var trip in trips) await _hub.JoinTrip(trip);

        await _hub.LeaveTrip(trips[0]);
        await _hub.JoinTrip(Guid.NewGuid());

        _groups.Verify(g => g.RemoveFromGroupAsync("conn-1", $"trip:{trips[0]}", It.IsAny<CancellationToken>()), Times.Once);
    }
}
