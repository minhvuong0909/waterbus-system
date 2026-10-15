using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.WebApi.Hubs;
using WaterbusSystem.WebApi.Realtime;
using Xunit;

namespace WaterbusSystem.UnitTests.Realtime;

/// <summary>
/// Kiểm tra publisher gửi đúng group + đúng tên event theo hợp đồng realtime (AGENTS.md).
/// </summary>
public class SignalRPublishersTests
{
    private static (Mock<IHubContext<THub>> Hub, Mock<IHubClients> Clients, Mock<IClientProxy> Proxy) CreateHub<THub>()
        where THub : Hub
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        clients.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<THub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        return (hub, clients, proxy);
    }

    private static void VerifySent(Mock<IClientProxy> proxy, string eventName, object payload) =>
        proxy.Verify(p => p.SendCoreAsync(
                eventName,
                It.Is<object?[]>(args => args.Length == 1 && Equals(args[0], payload)),
                It.IsAny<CancellationToken>()),
            Times.Once);

    [Fact]
    public async Task BoatPosition_ShouldGoToFleetAndBoatGroups()
    {
        var (hub, clients, proxy) = CreateHub<BoatTrackingHub>();
        var boatId = Guid.NewGuid();
        var message = new BoatPositionMessage(boatId, null, 10.7769m, 106.7009m, 8.5m, DateTimeOffset.UtcNow);

        await new SignalRBoatTrackingPublisher(hub.Object).PublishPositionAsync(message);

        clients.Verify(c => c.Groups(It.Is<IReadOnlyList<string>>(g =>
            g.Count == 2 && g.Contains("fleet") && g.Contains($"boat:{boatId}"))), Times.Once);
        VerifySent(proxy, "boatPosition", message);
    }

    [Theory]
    [InlineData("seatHeld")]
    [InlineData("seatReleased")]
    [InlineData("seatSold")]
    public async Task SeatEvents_ShouldGoToTripGroupWithMatchingEventName(string eventName)
    {
        var (hub, clients, proxy) = CreateHub<SeatSyncHub>();
        var tripId = Guid.NewGuid();
        var message = new SeatEventMessage(tripId, Guid.NewGuid(), 1, 3, "Held", DateTimeOffset.UtcNow.AddMinutes(10));
        var publisher = new SignalRSeatEventPublisher(hub.Object);

        var task = eventName switch
        {
            "seatHeld" => publisher.SeatHeldAsync(message),
            "seatReleased" => publisher.SeatReleasedAsync(message),
            _ => publisher.SeatSoldAsync(message)
        };
        await task;

        clients.Verify(c => c.Group($"trip:{tripId}"), Times.Once);
        VerifySent(proxy, eventName, message);
    }

    [Fact]
    public async Task WeatherAlert_ShouldGoOnlyToFleetGroup()
    {
        var (hub, clients, proxy) = CreateHub<BoatTrackingHub>();
        var message = new WeatherAlertMessage(Guid.NewGuid(), "High", Guid.NewGuid(), "Gió mạnh", DateTimeOffset.UtcNow);

        await new SignalROperationsAlertPublisher(hub.Object).PublishWeatherAlertAsync(message);

        clients.Verify(c => c.Group("fleet"), Times.Once);
        clients.Verify(c => c.Groups(It.IsAny<IReadOnlyList<string>>()), Times.Never);
        VerifySent(proxy, "weatherAlert", message);
    }

    [Fact]
    public void Payloads_ShouldExposeContractVersion()
    {
        new BoatPositionMessage(Guid.NewGuid(), null, 0, 0, null, DateTimeOffset.UtcNow).Version.Should().Be(1);
    }
}
