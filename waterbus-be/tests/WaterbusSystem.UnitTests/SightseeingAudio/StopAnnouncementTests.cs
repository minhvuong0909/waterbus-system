using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Services;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Validators.SightseeingAudio;
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
        options.SegmentDurationSeconds.Should().Be(180.0);
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
            .Setup(p => p.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing", It.IsAny<string?>(), default))
            .Returns(Task.CompletedTask);

        mockPublisher
            .Setup(p => p.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing", default))
            .Returns(Task.CompletedTask);

        mockPublisher
            .Setup(p => p.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing", default))
            .Returns(Task.CompletedTask);

        // Act: Rời bến Bạch Đằng -> Gần tới bến Bình An -> Đã tới bến Bình An
        await mockPublisher.Object.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing", nextStationName: null);
        await mockPublisher.Object.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing");
        await mockPublisher.Object.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing");

        // Assert
        mockPublisher.Verify(p => p.PublishDepartingAsync(tripId, boatId, fromStationId, "Bến Bạch Đằng", now, "Sightseeing", It.IsAny<string?>(), default), Times.Once);
        mockPublisher.Verify(p => p.PublishNearStopAsync(tripId, boatId, toStationId, "Bến Bình An", 0.8, now, "Sightseeing", default), Times.Once);
        mockPublisher.Verify(p => p.PublishArrivedAsync(tripId, boatId, toStationId, "Bến Bình An", 1.0, now, "Sightseeing", default), Times.Once);
    }

    [Fact]
    public void StopAnnouncementAdminService_GetConfiguration_ReturnsCurrentOptions()
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

        var service = new StopAnnouncementAdminService(optionsMock.Object);

        // Act
        var config = service.GetConfiguration();

        // Assert
        config.SegmentDurationSeconds.Should().Be(45);
        config.DwellTimeSeconds.Should().Be(5);
        config.NearThreshold.Should().Be(0.75);
        config.UseSimulatedSegmentDuration.Should().BeTrue();
    }

    [Fact]
    public void StopAnnouncementAdminService_UpdateConfiguration_UpdatesFieldsAndClampsNearThreshold()
    {
        // Arrange
        var currentOptions = new StopAnnouncementOptions();
        var optionsMock = new Mock<IOptionsMonitor<StopAnnouncementOptions>>();
        optionsMock.Setup(m => m.CurrentValue).Returns(currentOptions);

        var service = new StopAnnouncementAdminService(optionsMock.Object);

        // Act
        var request = new UpdateStopAnnouncementRequest(
            SegmentDurationSeconds: 15,
            DwellTimeSeconds: 2,
            NearThreshold: 1.5, // vượt quá 1.0 -> clamp về 0.99
            UseSimulatedSegmentDuration: false);

        var config = service.UpdateConfiguration(request);

        // Assert
        config.SegmentDurationSeconds.Should().Be(15);
        config.DwellTimeSeconds.Should().Be(2);
        config.NearThreshold.Should().Be(0.99);
        config.UseSimulatedSegmentDuration.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, true, "Thời gian mỗi chặng")]
    [InlineData(-5, true, "Thời gian mỗi chặng")]
    public void UpdateStopAnnouncementRequestValidator_InvalidSegmentDuration_FailsValidation(
        double segmentDuration, bool shouldFail, string errorSubstr)
    {
        // Arrange
        var validator = new UpdateStopAnnouncementRequestValidator();
        var request = new UpdateStopAnnouncementRequest(
            SegmentDurationSeconds: segmentDuration,
            DwellTimeSeconds: 3,
            NearThreshold: 0.8,
            UseSimulatedSegmentDuration: true);

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().NotBe(shouldFail);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(errorSubstr));
    }

    [Fact]
    public void AdminSimulatorController_DelegatesToService()
    {
        // Arrange
        var mockService = new Mock<IStopAnnouncementAdminService>();
        var expectedResponse = new StopAnnouncementConfigResponse(30, 3, 0.8, true);
        mockService.Setup(s => s.GetConfiguration()).Returns(expectedResponse);

        var controller = new AdminSimulatorController(mockService.Object);

        // Act
        var result = controller.GetStopAnnouncement();

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().Be(expectedResponse);
        mockService.Verify(s => s.GetConfiguration(), Times.Once);
    }
}
