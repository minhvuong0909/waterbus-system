using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

/// <summary>EF Core cho POI/audio; không gọi provider hay quyết định phát audio.</summary>
public sealed class SightseeingAudioRepository(ApplicationDbContext context) : ISightseeingAudioRepository
{
    private readonly ApplicationDbContext _context = context;

    public Task<bool> ActivePoiExistsAsync(Guid poiId, CancellationToken cancellationToken) =>
        _context.PointsOfInterest.AsNoTracking().AnyAsync(p => p.Id == poiId && !p.IsDeleted && p.IsActive, cancellationToken);

    public Task<AudioGuide?> FindGuideForUpdateAsync(Guid poiId, string languageCode, CancellationToken cancellationToken) =>
        _context.AudioGuides.FirstOrDefaultAsync(a => a.PoiId == poiId && a.LanguageCode == languageCode, cancellationToken);

    public void AddGuide(AudioGuide guide) => _context.AudioGuides.Add(guide);

    public async Task<AudioGuidePoisPageResponse> GetPoisAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var pois = _context.PointsOfInterest.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive);
        var total = await pois.CountAsync(cancellationToken);
        var page = await pois.OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        var poiIds = page.Select(p => p.Id).ToArray();
        var activeRoutePoiIds = await _context.RoutePois.AsNoTracking()
            .Where(rp => poiIds.Contains(rp.PoiId) && !rp.IsDeleted && rp.IsActive
                && rp.TriggerRadiusM.HasValue && rp.TriggerRadiusM > 0
                && rp.Route != null && !rp.Route.IsDeleted && rp.Route.IsActive)
            .Select(rp => rp.PoiId).Distinct().ToListAsync(cancellationToken);
        var guides = await _context.AudioGuides.AsNoTracking()
            .Where(a => poiIds.Contains(a.PoiId) && !a.IsDeleted && a.LanguageCode == "vi")
            .Select(a => new { a.Id, a.PoiId, a.AudioAssetUri, a.IsPublished })
            .ToListAsync(cancellationToken);

        var routePoiSet = activeRoutePoiIds.ToHashSet();
        var guideByPoiId = guides.ToDictionary(a => a.PoiId);
        var items = page.Select(p =>
        {
            guideByPoiId.TryGetValue(p.Id, out var guide);
            return new AudioGuidePoiResponse(p.Id, p.Name, routePoiSet.Contains(p.Id),
                guide?.Id, guide?.AudioAssetUri, guide?.IsPublished ?? false);
        }).ToArray();

        return new AudioGuidePoisPageResponse(items, pageNumber, pageSize, total);
    }

    public async Task<SightseeingAudioSnapshot?> GetTrackingSnapshotAsync(Guid tripId, Guid boatId, CancellationToken cancellationToken)
    {
        var trip = await _context.Trips.AsNoTracking()
            .Where(t => t.Id == tripId && !t.IsDeleted && t.BoatId == boatId)
            .Select(t => new { t.Id, t.BoatId, t.RouteId, t.TripType, t.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (trip is null)
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

        return new SightseeingAudioSnapshot(
            new TripSightseeingContext
            {
                TripId = trip.Id, BoatId = trip.BoatId!.Value, RouteId = trip.RouteId,
                TripType = trip.TripType, Status = trip.Status,
                IsSafetyAnnouncementActive = hasOpenSafetyIncident
                // Chưa có nguồn tiến độ RoutePoi/VisitId; core sẽ từ chối vùng chồng nhau.
            }, candidates);
    }

    public async Task<StopAnnouncementSnapshot?> GetStopAnnouncementSnapshotAsync(
        Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _context.Trips.AsNoTracking()
            .Where(t => t.Id == tripId && !t.IsDeleted)
            .Select(t => new { t.Id, t.BoatId, t.RouteId, t.TripType })
            .FirstOrDefaultAsync(cancellationToken);

        if (trip is null) return null;

        var stops = await _context.TripStopCalls.AsNoTracking()
            .Where(c => c.TripId == tripId && !c.IsDeleted && c.RouteStop != null && !c.RouteStop.IsDeleted
                && c.RouteStop.Station != null && !c.RouteStop.Station.IsDeleted)
            .OrderBy(c => c.VisitOrder)
            .Select(c => new StopAnnouncementStation(
                c.RouteStop!.Station!.Id,
                c.RouteStop.Station.Name,
                c.VisitOrder,
                (decimal)c.RouteStop.Station.Latitude,
                (decimal)c.RouteStop.Station.Longitude))
            .ToListAsync(cancellationToken);

        return new StopAnnouncementSnapshot(
            trip.Id, trip.BoatId ?? Guid.Empty, trip.RouteId, trip.TripType.ToString(), stops);
    }

    public async Task<IReadOnlyList<RouteNarrationCandidate>> ListRouteNarrationCandidatesAsync(
        Guid routeId, CancellationToken cancellationToken)
    {
        var routePois = await _context.RoutePois.AsNoTracking()
            .Where(rp => rp.RouteId == routeId && !rp.IsDeleted && rp.IsActive
                && rp.Poi != null && !rp.Poi.IsDeleted && rp.Poi.IsActive)
            .OrderBy(rp => rp.DisplayOrder)
            .Select(rp => new
            {
                rp.Id, rp.PoiId, rp.DisplayOrder, rp.Poi!.Name, rp.Poi.Description,
                rp.Poi.Latitude, rp.Poi.Longitude
            })
            .ToListAsync(cancellationToken);

        var poiIds = routePois.Select(rp => rp.PoiId).Distinct().ToArray();
        var guides = await _context.AudioGuides.AsNoTracking()
            .Where(a => poiIds.Contains(a.PoiId) && !a.IsDeleted && a.IsPublished && a.LanguageCode == "vi")
            .Select(a => new { a.Id, a.PoiId, a.AudioAssetUri, a.DurationSeconds })
            .ToListAsync(cancellationToken);

        var guideByPoiId = guides.ToDictionary(a => a.PoiId);

        return routePois.Select(rp =>
        {
            guideByPoiId.TryGetValue(rp.PoiId, out var guide);
            return new RouteNarrationCandidate(
                rp.Id, rp.PoiId, rp.Name, rp.Description, rp.Latitude, rp.Longitude, rp.DisplayOrder,
                guide?.Id, guide?.AudioAssetUri, guide?.DurationSeconds);
        }).ToArray();
    }

    public async Task<int> GetMaxRouteSegmentCountAsync(CancellationToken cancellationToken)
    {
        var stopCounts = await _context.RouteStops.AsNoTracking()
            .Where(rs => !rs.IsDeleted
                && rs.Route != null && !rs.Route.IsDeleted && rs.Route.IsActive)
            .GroupBy(rs => rs.RouteId)
            .Select(g => g.Count())
            .ToListAsync(cancellationToken);

        if (stopCounts.Count == 0) return 0;
        return Math.Max(0, stopCounts.Max() - 1);
    }
}
