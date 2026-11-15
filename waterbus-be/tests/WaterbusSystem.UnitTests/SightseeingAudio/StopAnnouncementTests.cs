using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class StopAnnouncementTests
{
    [Fact]
    public void StopAnnouncementOptions_DefaultValues_AreCorrectForDemo()
    {
        // Arrange & Act
        var options = new StopAnnouncementOptions();

        // Assert
        options.SegmentDurationSeconds.Should().Be(30.0);
        options.DwellTimeSeconds.Should().Be(3.0);
        options.NearThreshold.Should().Be(0.8);
        options.UseSimulatedSegmentDuration.Should().BeTrue();
    }

    [Fact]
    public void StopAnnouncementMessage_CarriesExpectedPayload()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        var boatId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var stationName = "Bến Bạch Đằng";
        var at = DateTimeOffset.UtcNow;

        // Act
        var message = new StopAnnouncementMessage(
            tripId, boatId, stationId, stationName, "near", 0.85, at, "Commuter");

        // Assert
        message.TripId.Should().Be(tripId);
        message.BoatId.Should().Be(boatId);
        message.StationId.Should().Be(stationId);
        message.StationName.Should().Be(stationName);
        message.Type.Should().Be("near");
        message.SegmentProgress.Should().Be(0.85);
        message.TripType.Should().Be("Commuter");
        message.Version.Should().Be(1);
    }

    [Fact]
    public async Task StopAnnouncementPublisher_PublishesAllThreeEventTypes()
    {
        // Arrange
        var mockPublisher = new Mock<IStopAnnouncementPublisher>();
        var tripId = Guid.NewGuid();
        var boatId = Guid.NewGuid();
        var fromStationId = Guid.NewGuid();
        var toStationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        mockPublisher
            .Setup(p => p.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing", default))
            .Returns(Task.CompletedTask);

        mockPublisher
            .Setup(p => p.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing", default))
            .Returns(Task.CompletedTask);

        mockPublisher
            .Setup(p => p.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing", default))
            .Returns(Task.CompletedTask);

        // Act: Rời bến Bạch Đằng -> Gần tới bến Bình An -> Đã tới bến Bình An
        await mockPublisher.Object.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing");
        await mockPublisher.Object.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing");
        await mockPublisher.Object.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing");

        // Assert
        mockPublisher.Verify(p => p.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing", default), Times.Once);
        mockPublisher.Verify(p => p.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing", default), Times.Once);
        mockPublisher.Verify(p => p.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing", default), Times.Once);
    }

    [Fact]
    public void AdminSimulatorController_GetConfig_ReturnsCurrentValues()
    {
        // Arrange
        var optionsMock = new Mock<IOptionsMonitor<StopAnnouncementOptions>>();
        var currentOptions = new StopAnnouncementOptions
        {
            SegmentDurationSeconds = 45,
            DwellTimeSeconds = 5,
            NearThreshold = 0.75,
            UseSimulatedSegmentDuration = true
        };
        optionsMock.Setup(m => m.CurrentValue).Returns(currentOptions);

        var controller = new AdminSimulatorController(optionsMock.Object);

        // Act
        var result = controller.GetStopAnnouncement();

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        var body = okResult!.Value as StopAnnouncementConfigResponse;
        body.Should().NotBeNull();
        body!.SegmentDurationSeconds.Should().Be(45);
        body.DwellTimeSeconds.Should().Be(5);
        body.NearThreshold.Should().Be(0.75);
        body.UseSimulatedSegmentDuration.Should().BeTrue();
    }

    [Fact]
    public void AdminSimulatorController_UpdateConfig_ModifiesConfigAndClampsThreshold()
    {
        // Arrange
        var currentOptions = new StopAnnouncementOptions();
        var optionsMock = new Mock<IOptionsMonitor<StopAnnouncementOptions>>();
        optionsMock.Setup(m => m.CurrentValue).Returns(currentOptions);

        var controller = new AdminSimulatorController(optionsMock.Object);

        // Act: cập nhật với threshold vượt biên (1.5 -> clamp về 0.99)
        var request = new UpdateStopAnnouncementRequest(
            SegmentDurationSeconds: 15,
            DwellTimeSeconds: 2,
            NearThreshold: 1.5,
            UseSimulatedSegmentDuration: true);

        var actionResult = controller.UpdateStopAnnouncement(request);

        // Assert
        var okResult = actionResult as OkObjectResult;
        okResult.Should().NotBeNull();
        var body = okResult!.Value as StopAnnouncementConfigResponse;
        body.Should().NotBeNull();
        body!.SegmentDurationSeconds.Should().Be(15);
        body.DwellTimeSeconds.Should().Be(2);
        body.NearThreshold.Should().Be(0.99); // Đã được clamp
        currentOptions.SegmentDurationSeconds.Should().Be(15);
    }

    [Fact]
    public void AdminSimulatorController_UpdateConfig_PreservesExistingWhenNull()
    {
        // Arrange
        var currentOptions = new StopAnnouncementOptions
        {
            SegmentDurationSeconds = 30,
            DwellTimeSeconds = 3,
            NearThreshold = 0.8
        };
        var optionsMock = new Mock<IOptionsMonitor<StopAnnouncementOptions>>();
        optionsMock.Setup(m => m.CurrentValue).Returns(currentOptions);

        var controller = new AdminSimulatorController(optionsMock.Object);

        // Act: chỉ cập nhật SegmentDurationSeconds, các field khác null
        var request = new UpdateStopAnnouncementRequest(
            SegmentDurationSeconds: 20,
            DwellTimeSeconds: null,
            NearThreshold: null,
            UseSimulatedSegmentDuration: null);

        var actionResult = controller.UpdateStopAnnouncement(request);

        // Assert
        var okResult = actionResult as OkObjectResult;
        okResult.Should().NotBeNull();
        var body = okResult!.Value as StopAnnouncementConfigResponse;
        body.Should().NotBeNull();
        body!.SegmentDurationSeconds.Should().Be(20);
        body.DwellTimeSeconds.Should().Be(3); // Giữ nguyên
        body.NearThreshold.Should().Be(0.8); // Giữ nguyên
    }
}
