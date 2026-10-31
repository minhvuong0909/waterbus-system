using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.DTOs.Responses.Tracking;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Tracking.Services;

/// <summary>
/// Theo dõi vị trí tàu. GPS của Captain, bản giả lập và radar Admin đều đi qua đây (BR-TRK-01).
/// </summary>
public sealed class BoatTrackingService(
    IBoatRepository boats,
    IBoatPositionRepository positions,
    ITripRepository trips,
    IUnitOfWork unitOfWork,
    IBoatPositionCache cache,
    IBoatTrackingPublisher publisher,
    ITrackingAudioTriggerEvaluator audioEvaluator,
    IValidator<RecordBoatPositionRequest> recordValidator,
    IValidator<GetBoatPositionHistoryRequest> historyValidator,
    IOptions<TrackingOptions> options,
    IOptions<SightseeingAudioOptions> audioOptions,
    ILogger<BoatTrackingService> logger) : IBoatTrackingService
{
    // BR-INC-05: Trip Suspended vẫn tiếp tục cung cấp vị trí
    private static readonly TripStatus[] TrackableTripStatuses =
        [TripStatus.Boarding, TripStatus.EnRoute, TripStatus.Suspended];

    private readonly TrackingOptions _options = options.Value;
    private readonly SightseeingAudioOptions _audioOptions = audioOptions.Value;

    public async Task<RecordBoatPositionResponse> RecordPositionAsync(
        RecordBoatPositionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await recordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var boat = await boats.GetActiveForTrackingAsync(request.BoatId, cancellationToken);

        if (request.Source == BoatPositionSources.Gps
            && (request.ReportedByCaptainId is null || boat.CaptainUserId != request.ReportedByCaptainId))
        {
            throw new ForbiddenAccessException("Chỉ thuyền trưởng được gán cho tàu này mới được gửi vị trí GPS.");
        }

        // Bỏ qua điểm đến trễ/sai thứ tự để marker trên radar không bị giật lùi
        var latest = await cache.GetAsync(request.BoatId, cancellationToken);
        if (latest is not null && request.ObservedAt <= latest.ObservedAt)
        {
            return new RecordBoatPositionResponse(false, false, latest.TripId);
        }

        var tripId = await trips.FindLatestTripIdByBoatAsync(request.BoatId, TrackableTripStatuses, cancellationToken);

        var message = new BoatPositionMessage(
            request.BoatId, tripId, request.Latitude, request.Longitude, request.SpeedKnots, request.ObservedAt);

        var persisted = await PersistIfDueAsync(request, tripId, cancellationToken);

        await cache.SetAsync(message, cancellationToken);
        await publisher.PublishPositionAsync(message, cancellationToken);

        if (_audioOptions.EnableGpsTriggerEvaluation && tripId.HasValue)
        {
            await EvaluateAudioGeofenceAsync(request, tripId.Value, cancellationToken);
        }

        return new RecordBoatPositionResponse(true, persisted, tripId);
    }

    public async Task<List<BoatLatestPositionResponse>> GetLatestPositionsAsync(CancellationToken cancellationToken = default)
    {
        var activeBoats = await boats.ListActiveSummariesAsync(cancellationToken);
        var cached = (await cache.GetAllAsync(cancellationToken)).ToDictionary(m => m.BoatId);

        var result = new List<BoatLatestPositionResponse>();
        foreach (var boat in activeBoats)
        {
            if (cached.TryGetValue(boat.Id, out var position))
            {
                result.Add(new BoatLatestPositionResponse(boat.Id, boat.Code, boat.Name, position.TripId,
                    position.Lat, position.Lng, position.SpeedKnots, position.ObservedAt));
                continue;
            }

            var persisted = await positions.GetLatestAsync(boat.Id, cancellationToken);
            if (persisted is not null)
            {
                result.Add(new BoatLatestPositionResponse(boat.Id, boat.Code, boat.Name, persisted.TripId,
                    persisted.Latitude, persisted.Longitude, persisted.SpeedKnots, persisted.ObservedAt));
            }
        }

        return result;
    }

    public async Task<PaginatedList<BoatPositionHistoryResponse>> GetPositionHistoryAsync(
        GetBoatPositionHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await historyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        if (!await boats.ExistsAsync(request.BoatId, cancellationToken))
        {
            throw new Domain.Exceptions.NotFoundException(nameof(Boat), request.BoatId);
        }

        return await positions.GetHistoryPageAsync(request, cancellationToken);
    }

    /// <summary>
    /// Ghi SQL thưa (mỗi PersistIntervalSeconds) để lưu lịch sử mà không ghi mọi tick GPS.
    /// </summary>
    private async Task<bool> PersistIfDueAsync(RecordBoatPositionRequest request, Guid? tripId, CancellationToken cancellationToken)
    {
        var lastPersistedAt = await positions.GetLastPersistedAtAsync(request.BoatId, cancellationToken);

        if (lastPersistedAt is not null
            && request.ObservedAt - lastPersistedAt.Value < TimeSpan.FromSeconds(_options.PersistIntervalSeconds))
        {
            return false;
        }

        positions.Add(new BoatPositionEvent
        {
            BoatId = request.BoatId,
            TripId = tripId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            SpeedKnots = request.SpeedKnots,
            ObservedAt = request.ObservedAt,
            Source = request.Source
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EvaluateAudioGeofenceAsync(RecordBoatPositionRequest request, Guid tripId, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await audioEvaluator.EvaluateAsync(new BoatPositionSnapshot
            {
                BoatId = request.BoatId, TripId = tripId, Latitude = request.Latitude,
                Longitude = request.Longitude, SpeedKnots = request.SpeedKnots,
                ObservedAt = request.ObservedAt, Source = request.Source
            }, cancellationToken);
            if (outcome is not null)
                logger.LogInformation("Audio geofence Trip={TripId}: {Reason}; dispatched={Dispatched}",
                    tripId, outcome.Reason, outcome.DispatchResult?.IsDispatched ?? false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Audio tùy chọn không được làm mất GPS/radar đang hoạt động.
            logger.LogWarning(ex, "Không đánh giá được audio geofence cho Trip {TripId}.", tripId);
        }
    }
}
