using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using ValidationFailure = FluentValidation.Results.ValidationFailure;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Services;

/// <summary>
/// Quản trị cấu hình thời gian chuyến và ngưỡng báo hiệu bến.
/// Mọi cập nhật phải vượt qua ngân sách thời gian: không có cấu hình nào được phép khiến
/// thông báo rời bến, thuyết minh địa danh và thông báo gần bến chồng lên nhau trên tàu.
/// </summary>
public sealed class StopAnnouncementAdminService(
    IStopAnnouncementConfigStore configStore,
    ISightseeingAudioRepository repository) : IStopAnnouncementAdminService
{
    public async Task<StopAnnouncementConfigResponse> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var options = configStore.Current;
        var budget = await BuildBudgetAsync(options, cancellationToken);
        return ToResponse(options, budget);
    }

    public async Task<StopAnnouncementConfigResponse> UpdateConfigurationAsync(
        UpdateStopAnnouncementRequest request, CancellationToken cancellationToken = default)
    {
        var current = configStore.Current;
        var candidate = current.Clone();

        if (request.TripDurationSeconds.HasValue)
            candidate.TripDurationSeconds = request.TripDurationSeconds.Value;
        if (request.DwellTimeSeconds.HasValue)
            candidate.DwellTimeSeconds = request.DwellTimeSeconds.Value;
        if (request.NearThreshold.HasValue)
            candidate.NearThreshold = request.NearThreshold.Value;
        if (request.UseSimulatedSegmentDuration.HasValue)
            candidate.UseSimulatedSegmentDuration = request.UseSimulatedSegmentDuration.Value;

        var segmentCount = await ResolveSegmentCountAsync(cancellationToken);
        var budget = StopAnnouncementTimeBudgetCalculator.Calculate(candidate, segmentCount);

        if (!budget.IsValid)
        {
            throw new ValidationException(
                budget.Violations
                    .Select(v => new ValidationFailure(v.Property, v.Message))
                    .ToArray());
        }

        configStore.Replace(candidate);
        return ToResponse(candidate, budget);
    }

    private async Task<StopAnnouncementTimeBudget> BuildBudgetAsync(
        StopAnnouncementOptions options, CancellationToken cancellationToken)
    {
        var segmentCount = await ResolveSegmentCountAsync(cancellationToken);
        return StopAnnouncementTimeBudgetCalculator.Calculate(options, segmentCount);
    }

    private async Task<int> ResolveSegmentCountAsync(CancellationToken cancellationToken)
    {
        var maxSegments = await repository.GetMaxRouteSegmentCountAsync(cancellationToken);
        return maxSegments > 0 ? maxSegments : Math.Max(1, configStore.Current.EstimatedSegmentCount);
    }

    private static StopAnnouncementConfigResponse ToResponse(
        StopAnnouncementOptions options, StopAnnouncementTimeBudget budget) =>
        new(
            options.TripDurationSeconds,
            options.DwellTimeSeconds,
            options.NearThreshold,
            options.UseSimulatedSegmentDuration,
            new StopAnnouncementBudgetResponse(
                budget.SegmentCount,
                budget.SegmentMovingSeconds,
                budget.DepartingWindowSeconds,
                budget.NearWindowSeconds,
                budget.DwellSeconds,
                budget.MaxNarrationSeconds,
                budget.MaxNarrationCharacters,
                budget.MinDwellSeconds,
                budget.MinTripDurationSeconds,
                budget.IsValid,
                budget.Violations
                    .Select(v => new StopAnnouncementBudgetViolationResponse(v.Property, v.Message))
                    .ToArray()));
}
