using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Services;

/// <summary>Adapter dữ liệu tracking/DB cho geofence; không tạo nội dung POI hoặc phát loa.</summary>
public sealed class TrackingAudioTriggerEvaluator : ITrackingAudioTriggerEvaluator
{
    private readonly IApplicationDbContext _context;
    private readonly IPoiAudioTriggerDecisionService _decisionService;

    public TrackingAudioTriggerEvaluator(IApplicationDbContext context, IPoiAudioTriggerDecisionService decisionService)
    {
        _context = context;
        _decisionService = decisionService;
    }

    public async Task<PoiAudioTriggerEvaluationResult?> EvaluateAsync(
        BoatPositionSnapshot position, CancellationToken cancellationToken = default)
    {
        if (position.TripId is not Guid tripId) return null;

        var trip = await _context.Trips.AsNoTracking()
            .Where(t => t.Id == tripId && !t.IsDeleted && t.BoatId == position.BoatId)
            .Select(t => new { t.Id, t.BoatId, t.RouteId, t.TripType, t.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (trip is null || trip.TripType != TripType.Sightseeing || trip.Status != TripStatus.EnRoute)
            return null;

        // Một SOS an toàn còn mở phải chặn lệnh mới, dù Admin chưa đổi Trip.Status.
        var hasOpenSafetyIncident = await _context.IncidentTrips.AsNoTracking()
            .AnyAsync(link => link.TripId == tripId && !link.IsDeleted &&
                link.Incident != null && !link.Incident.IsDeleted &&
                link.Incident.IncidentType == IncidentConstants.Types.Safety &&
                link.Incident.Status == IncidentConstants.Statuses.Open, cancellationToken);

        var routePois = await _context.RoutePois.AsNoTracking()
            .Where(rp => rp.RouteId == trip.RouteId && !rp.IsDeleted)
            .Select(rp => new
            {
                rp.Id, rp.PoiId, rp.RouteId, rp.DisplayOrder, rp.TriggerRadiusM, rp.IsActive,
                PoiName = rp.Poi!.Name,
                PoiLatitude = rp.Poi.Latitude,
                PoiLongitude = rp.Poi.Longitude,
                PoiIsActive = rp.Poi.IsActive && !rp.Poi.IsDeleted
            })
            .ToListAsync(cancellationToken);

        var poiIds = routePois.Select(rp => rp.PoiId).Distinct().ToArray();
        var audioGuides = await _context.AudioGuides.AsNoTracking()
            .Where(a => poiIds.Contains(a.PoiId) && !a.IsDeleted)
            .Select(a => new AudioGuideCandidate
            {
                AudioGuideId = a.Id, PoiId = a.PoiId, LanguageCode = a.LanguageCode,
                AudioAssetUri = a.AudioAssetUri, DurationSeconds = a.DurationSeconds,
                IsPublished = a.IsPublished
            })
            .ToListAsync(cancellationToken);

        var candidates = routePois.Select(rp => new PoiGeofenceCandidate
        {
            RoutePoiId = rp.Id, PoiId = rp.PoiId, RouteId = rp.RouteId,
            PoiName = rp.PoiName, Latitude = rp.PoiLatitude, Longitude = rp.PoiLongitude,
            TriggerRadiusM = rp.TriggerRadiusM, DisplayOrder = rp.DisplayOrder,
            IsPoiActive = rp.PoiIsActive, IsRoutePoiActive = rp.IsActive,
            AudioGuides = audioGuides.Where(a => a.PoiId == rp.PoiId).ToArray()
        }).ToArray();

        return await _decisionService.EvaluateAndTriggerAsync(position,
            new TripSightseeingContext
            {
                TripId = trip.Id, BoatId = trip.BoatId!.Value, RouteId = trip.RouteId,
                TripType = trip.TripType, Status = trip.Status,
                IsSafetyAnnouncementActive = hasOpenSafetyIncident
                // Chưa có nguồn tiến độ RoutePoi/VisitId; core sẽ từ chối vùng chồng nhau.
            }, candidates, cancellationToken);
    }
}
