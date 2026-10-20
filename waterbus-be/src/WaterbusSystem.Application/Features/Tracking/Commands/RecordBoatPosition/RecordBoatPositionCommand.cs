using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Tracking.Commands.RecordBoatPosition;

public static class BoatPositionSources
{
    public const string Gps = "GPS";
    public const string Simulated = "Simulated";
}

/// <summary>
/// Kết quả ghi nhận vị trí: Accepted = đã phát realtime; Persisted = đã ghi BoatPositionEvent.
/// </summary>
public record RecordBoatPositionResult(bool Accepted, bool Persisted, Guid? TripId);

/// <summary>
/// Ghi nhận 1 vị trí của Boat (BR-TRK-01: GPS của tàu, hoặc giả lập theo Route khi demo).
/// Nguồn GPS bắt buộc có ReportedByCaptainId và phải là Captain được gán cho Boat.
/// </summary>
public record RecordBoatPositionCommand(
    Guid BoatId,
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKnots,
    DateTimeOffset ObservedAt,
    string Source,
    Guid? ReportedByCaptainId) : IRequest<RecordBoatPositionResult>;

public class RecordBoatPositionCommandValidator : AbstractValidator<RecordBoatPositionCommand>
{
    public RecordBoatPositionCommandValidator(IDateTimeService dateTime, IOptions<TrackingOptions> options)
    {
        RuleFor(x => x.BoatId).NotEmpty().WithMessage("Thiếu mã tàu.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m).WithMessage("Vĩ độ phải nằm trong [-90, 90].");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m).WithMessage("Kinh độ phải nằm trong [-180, 180].");

        RuleFor(x => x.SpeedKnots)
            .InclusiveBetween(0m, 100m).When(x => x.SpeedKnots.HasValue)
            .WithMessage("Tốc độ (hải lý/giờ) phải nằm trong [0, 100].");

        RuleFor(x => x.ObservedAt)
            .Must(t => t <= dateTime.Now.AddMinutes(1))
            .WithMessage("Thời điểm ghi nhận vị trí không được ở tương lai.")
            .Must(t => t >= dateTime.Now.AddMinutes(-options.Value.MaxPositionAgeMinutes))
            .WithMessage("Vị trí quá cũ, không được ghi nhận.");

        RuleFor(x => x.Source)
            .Must(s => s is BoatPositionSources.Gps or BoatPositionSources.Simulated)
            .WithMessage("Nguồn vị trí chỉ được là GPS hoặc Simulated.");
    }
}

public class RecordBoatPositionCommandHandler : IRequestHandler<RecordBoatPositionCommand, RecordBoatPositionResult>
{
    // BR-INC-05: Trip Suspended vẫn tiếp tục cung cấp vị trí
    private static readonly TripStatus[] TrackableTripStatuses =
        [TripStatus.Boarding, TripStatus.EnRoute, TripStatus.Suspended];

    private readonly IApplicationDbContext _context;
    private readonly IBoatPositionCache _cache;
    private readonly IBoatTrackingPublisher _publisher;
    private readonly TrackingOptions _options;
    private readonly SightseeingAudioOptions _audioOptions;
    private readonly ITrackingAudioTriggerEvaluator _audioEvaluator;
    private readonly ILogger<RecordBoatPositionCommandHandler> _logger;

    public RecordBoatPositionCommandHandler(
        IApplicationDbContext context,
        IBoatPositionCache cache,
        IBoatTrackingPublisher publisher,
        IOptions<TrackingOptions> options,
        IOptions<SightseeingAudioOptions> audioOptions,
        ITrackingAudioTriggerEvaluator audioEvaluator,
        ILogger<RecordBoatPositionCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _publisher = publisher;
        _options = options.Value;
        _audioOptions = audioOptions.Value;
        _audioEvaluator = audioEvaluator;
        _logger = logger;
    }

    public async Task<RecordBoatPositionResult> Handle(RecordBoatPositionCommand request, CancellationToken cancellationToken)
    {
        var boat = await _context.Boats.AsNoTracking()
            .Where(b => b.Id == request.BoatId && !b.IsDeleted && b.IsActive)
            .Select(b => new { b.Id, b.CaptainUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Boat), request.BoatId);

        if (request.Source == BoatPositionSources.Gps
            && (request.ReportedByCaptainId is null || boat.CaptainUserId != request.ReportedByCaptainId))
        {
            throw new ForbiddenAccessException("Chỉ thuyền trưởng được gán cho tàu này mới được gửi vị trí GPS.");
        }

        // Bỏ qua điểm đến trễ/sai thứ tự để marker trên radar không bị giật lùi
        var latest = await _cache.GetAsync(request.BoatId, cancellationToken);
        if (latest is not null && request.ObservedAt <= latest.ObservedAt)
        {
            return new RecordBoatPositionResult(false, false, latest.TripId);
        }

        var tripId = await _context.Trips.AsNoTracking()
            .Where(t => t.BoatId == request.BoatId && !t.IsDeleted && TrackableTripStatuses.Contains(t.Status))
            .OrderByDescending(t => t.DepartureTime)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var message = new BoatPositionMessage(
            request.BoatId, tripId, request.Latitude, request.Longitude, request.SpeedKnots, request.ObservedAt);

        var persisted = await PersistIfDueAsync(request, tripId, cancellationToken);

        await _cache.SetAsync(message, cancellationToken);
        await _publisher.PublishPositionAsync(message, cancellationToken);

        if (_audioOptions.EnableGpsTriggerEvaluation && tripId.HasValue)
        {
            try
            {
                var outcome = await _audioEvaluator.EvaluateAsync(new BoatPositionSnapshot
                {
                    BoatId = request.BoatId, TripId = tripId, Latitude = request.Latitude,
                    Longitude = request.Longitude, SpeedKnots = request.SpeedKnots,
                    ObservedAt = request.ObservedAt, Source = request.Source
                }, cancellationToken);
                if (outcome is not null)
                    _logger.LogInformation("Audio geofence Trip={TripId}: {Reason}; dispatched={Dispatched}",
                        tripId, outcome.Reason, outcome.DispatchResult?.IsDispatched ?? false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Audio tùy chọn không được làm mất GPS/radar đang hoạt động.
                _logger.LogWarning(ex, "Không đánh giá được audio geofence cho Trip {TripId}.", tripId);
            }
        }

        return new RecordBoatPositionResult(true, persisted, tripId);
    }

    /// <summary>
    /// Ghi SQL thưa (mỗi PersistIntervalSeconds) để lưu lịch sử mà không ghi mọi tick GPS.
    /// </summary>
    private async Task<bool> PersistIfDueAsync(RecordBoatPositionCommand request, Guid? tripId, CancellationToken cancellationToken)
    {
        var lastPersistedAt = await _context.BoatPositionEvents.AsNoTracking()
            .Where(e => e.BoatId == request.BoatId)
            .OrderByDescending(e => e.ObservedAt)
            .Select(e => (DateTimeOffset?)e.ObservedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastPersistedAt is not null
            && request.ObservedAt - lastPersistedAt.Value < TimeSpan.FromSeconds(_options.PersistIntervalSeconds))
        {
            return false;
        }

        _context.BoatPositionEvents.Add(new BoatPositionEvent
        {
            BoatId = request.BoatId,
            TripId = tripId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            SpeedKnots = request.SpeedKnots,
            ObservedAt = request.ObservedAt,
            Source = request.Source
        });
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
