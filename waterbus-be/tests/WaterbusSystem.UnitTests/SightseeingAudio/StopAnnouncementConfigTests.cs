using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Services;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Validators.SightseeingAudio;
using WaterbusSystem.Infrastructure.Services.SightseeingAudio;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

/// <summary>
/// Kiểm tra ngân sách thời gian và cấu hình thông báo bến mà Admin điều chỉnh.
/// </summary>
public class StopAnnouncementConfigTests
{
    [Fact]
    public void StopAnnouncementOptions_DefaultValues_AreCorrectForDemo()
    {
        var options = new StopAnnouncementOptions();

        options.TripDurationSeconds.Should().Be(180.0);
        options.DwellTimeSeconds.Should().Be(6.0);
        options.NearThreshold.Should().Be(0.75);
        options.UseSimulatedSegmentDuration.Should().BeTrue();
    }

    [Fact]
    public async Task StopAnnouncementAdminService_GetConfiguration_ReturnsCurrentOptionsAndBudget()
    {
        var store = new StopAnnouncementConfigStore(new StopAnnouncementOptions
        {
            TripDurationSeconds = 180,
            DwellTimeSeconds = 6,
            NearThreshold = 0.75,
            UseSimulatedSegmentDuration = true
        });
        var service = new StopAnnouncementAdminService(store, RepositoryWithSegments(4));

        var config = await service.GetConfigurationAsync();

        config.TripDurationSeconds.Should().Be(180);
        config.DwellTimeSeconds.Should().Be(6);
        config.NearThreshold.Should().Be(0.75);
        config.UseSimulatedSegmentDuration.Should().BeTrue();
        config.Budget.SegmentCount.Should().Be(4);
        config.Budget.IsValid.Should().BeTrue();
        config.Budget.MaxNarrationSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StopAnnouncementAdminService_UpdateConfiguration_RejectsTripTooShortForAudio()
    {
        var store = new StopAnnouncementConfigStore(new StopAnnouncementOptions());
        var service = new StopAnnouncementAdminService(store, RepositoryWithSegments(4));

        var act = async () => await service.UpdateConfigurationAsync(new UpdateStopAnnouncementRequest(
            TripDurationSeconds: 30,
            DwellTimeSeconds: 5,
            NearThreshold: 0.75,
            UseSimulatedSegmentDuration: true));

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Errors.ContainsKey("TripDurationSeconds"));
    }

    [Fact]
    public async Task StopAnnouncementAdminService_UpdateConfiguration_RejectsDwellTooShortForArrivedAnnouncement()
    {
        var store = new StopAnnouncementConfigStore(new StopAnnouncementOptions());
        var service = new StopAnnouncementAdminService(store, RepositoryWithSegments(4));

        var act = async () => await service.UpdateConfigurationAsync(new UpdateStopAnnouncementRequest(
            TripDurationSeconds: 180,
            DwellTimeSeconds: 1,
            NearThreshold: 0.75,
            UseSimulatedSegmentDuration: true));

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Errors.ContainsKey("DwellTimeSeconds"));
    }

    [Fact]
    public async Task StopAnnouncementAdminService_UpdateConfiguration_AppliesValidChange()
    {
        var store = new StopAnnouncementConfigStore(new StopAnnouncementOptions());
        var service = new StopAnnouncementAdminService(store, RepositoryWithSegments(4));

        var config = await service.UpdateConfigurationAsync(new UpdateStopAnnouncementRequest(
            TripDurationSeconds: 240,
            DwellTimeSeconds: 8,
            NearThreshold: 0.8,
            UseSimulatedSegmentDuration: true));

        config.TripDurationSeconds.Should().Be(240);
        config.DwellTimeSeconds.Should().Be(8);
        config.NearThreshold.Should().Be(0.8);
        store.Current.TripDurationSeconds.Should().Be(240);
    }

    [Fact]
    public void StopAnnouncementTimeBudget_TripDuration30Seconds_FitsNoAudioAtAll()
    {
        var budget = StopAnnouncementTimeBudgetCalculator.Calculate(
            new StopAnnouncementOptions { TripDurationSeconds = 30, DwellTimeSeconds = 5, NearThreshold = 0.75 }, 4);

        budget.IsValid.Should().BeFalse();
        budget.MaxNarrationSeconds.Should().Be(0);
        budget.MinTripDurationSeconds.Should().BeGreaterThan(30);
    }

    [Fact]
    public void StopAnnouncementTimeBudget_ValidConfig_LeavesRoomForEveryAnnouncement()
    {
        var budget = StopAnnouncementTimeBudgetCalculator.Calculate(
            new StopAnnouncementOptions { TripDurationSeconds = 180, DwellTimeSeconds = 6, NearThreshold = 0.75 }, 4);

        budget.IsValid.Should().BeTrue();
        budget.DepartingWindowSeconds.Should().BeGreaterThanOrEqualTo(
            budget.MaxNarrationSeconds + new StopAnnouncementOptions().DepartingAnnouncementSeconds);
        budget.NearWindowSeconds.Should().BeGreaterThanOrEqualTo(
            new StopAnnouncementOptions().NearAnnouncementSeconds);
        budget.DwellSeconds.Should().BeGreaterThanOrEqualTo(
            new StopAnnouncementOptions().ArrivedAnnouncementSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(30)]
    public void UpdateStopAnnouncementRequestValidator_InvalidTripDuration_FailsValidation(double tripDuration)
    {
        var validator = new UpdateStopAnnouncementRequestValidator();
        var request = new UpdateStopAnnouncementRequest(
            TripDurationSeconds: tripDuration,
            DwellTimeSeconds: 3,
            NearThreshold: 0.8,
            UseSimulatedSegmentDuration: true);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Thời lượng toàn chuyến"));
    }

    [Fact]
    public void UpdateStopAnnouncementRequestValidator_ValidTripDuration_PassesValidation()
    {
        var validator = new UpdateStopAnnouncementRequestValidator();
        var request = new UpdateStopAnnouncementRequest(
            TripDurationSeconds: 180,
            DwellTimeSeconds: 5,
            NearThreshold: 0.8,
            UseSimulatedSegmentDuration: true);

        validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AdminSimulatorController_DelegatesToService()
    {
        var mockService = new Mock<IStopAnnouncementAdminService>();
        var expectedResponse = new StopAnnouncementConfigResponse(180, 6, 0.75, true, null!);
        mockService.Setup(s => s.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new AdminSimulatorController(mockService.Object);

        var result = await controller.GetStopAnnouncement(CancellationToken.None);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().Be(expectedResponse);
        mockService.Verify(s => s.GetConfigurationAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ISightseeingAudioRepository RepositoryWithSegments(int segmentCount)
    {
        var repository = new Mock<ISightseeingAudioRepository>();
        repository.Setup(r => r.GetMaxRouteSegmentCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(segmentCount);
        return repository.Object;
    }
}
