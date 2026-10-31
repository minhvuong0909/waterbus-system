using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Responses.Incidents;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Incidents.Services;

public sealed class WeatherGuardService(
    ITripRepository trips,
    IRouteRepository routes,
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    IWeatherGuardRunLock runLock,
    IWeatherProvider weather,
    IOperationsAlertPublisher alerts,
    IDateTimeService dateTime,
    IOptions<WeatherOptions> options,
    ILogger<WeatherGuardService> logger) : IWeatherGuardService
{
    private static readonly TripStatus[] ActiveTripStatuses =
        [TripStatus.Scheduled, TripStatus.Boarding, TripStatus.EnRoute, TripStatus.Suspended];

    private readonly WeatherOptions _options = options.Value;

    /// <summary>Kết quả đánh giá một tuyến: quan sát tệ nhất và số lần lấy thời tiết thất bại.</summary>
    private sealed record RouteAssessment(WeatherObservation? Worst, string? Severity, bool ObservedAll, int NewFailures);

    /// <summary>Thời gian chờ lượt khác xong; lâu hơn thì báo bận thay vì chạy chồng.</summary>
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(30);

    public async Task<WeatherGuardResponse> RunAsync(CancellationToken cancellationToken = default)
    {
        // Audit M09: lượt thủ công và lượt Hangfire không được chạy chồng nhau (Incident tạo trùng cho cùng tuyến)
        await using var gate = await runLock.TryAcquireAsync(LockWait, cancellationToken)
            ?? throw new ConflictException("Đang có một lượt kiểm tra thời tiết khác chạy. Vui lòng thử lại sau ít phút.");

        var now = dateTime.Now;

        var activeTrips = await trips.ListActiveInWindowAsync(
            ActiveTripStatuses, now, now.AddHours(_options.LookaheadHours), cancellationToken);
        var tripsByRoute = activeTrips
            .GroupBy(t => t.RouteId)
            .ToDictionary(g => g.Key, g => g.Select(t => t.TripId).ToList());

        if (tripsByRoute.Count == 0)
        {
            // Không còn chuyến nào cần theo dõi: các cảnh báo thời tiết đang mở không còn ý nghĩa
            var closed = await ResolveOpenIncidentsAsync(_ => true, now, cancellationToken);
            return new WeatherGuardResponse(0, 0, 0, 0, closed);
        }

        var monitoredRoutes = await routes.ListByIdsWithStationPointsAsync(tripsByRoute.Keys, cancellationToken);

        // Nhiều tuyến dùng chung bến: mỗi toạ độ (làm tròn ~1 km) chỉ gọi API 1 lần trong 1 lượt quét
        var observationCache = new Dictionary<(double, double), WeatherObservation?>();
        int created = 0, updated = 0, failures = 0;
        var safeRouteIds = new HashSet<Guid>();

        foreach (var route in monitoredRoutes)
        {
            var assessment = await AssessRouteAsync(route, observationCache, cancellationToken);
            failures += assessment.NewFailures;

            if (assessment.Worst is null || assessment.Severity is null)
            {
                // Chỉ coi là "an toàn" khi lấy được thời tiết ở mọi bến; thiếu dữ liệu thì giữ nguyên cảnh báo
                if (assessment.ObservedAll) safeRouteIds.Add(route.Id);
                continue;
            }

            var isNew = await RecordIncidentAsync(
                route.Id, route.Code, assessment.Worst, assessment.Severity, tripsByRoute[route.Id], now, cancellationToken);
            if (isNew) created++; else updated++;
        }

        // Tuyến an toàn, hoặc tuyến không còn chuyến cần theo dõi -> đóng cảnh báo cũ
        var monitoredRouteIds = monitoredRoutes.Select(r => r.Id).ToHashSet();
        var resolved = await ResolveOpenIncidentsAsync(
            i => i.RouteId is { } id && (safeRouteIds.Contains(id) || !monitoredRouteIds.Contains(id)),
            now, cancellationToken);

        logger.LogInformation(
            "[WEATHER GUARD] Đã kiểm tra {Routes} tuyến: tạo {Created}, cập nhật {Updated}, đóng {Resolved} Incident, {Failures} lần lấy thời tiết thất bại.",
            monitoredRoutes.Count, created, updated, resolved, failures);

        return new WeatherGuardResponse(monitoredRoutes.Count, created, updated, failures, resolved);
    }

    /// <summary>Hỏi thời tiết ở từng bến của tuyến (có cache theo toạ độ) và lấy mức nặng nhất.</summary>
    private async Task<RouteAssessment> AssessRouteAsync(
        RouteStationPoints route,
        Dictionary<(double, double), WeatherObservation?> cache,
        CancellationToken cancellationToken)
    {
        WeatherObservation? worst = null;
        string? worstSeverity = null;
        var observedAll = true;
        var newFailures = 0;

        foreach (var point in route.RoundedDistinctPoints())
        {
            if (!cache.TryGetValue(point, out var observation))
            {
                observation = await TryObserveAsync(point.Lat, point.Lng, cancellationToken);
                if (observation is null) newFailures++;
                cache[point] = observation;
            }

            if (observation is null)
            {
                observedAll = false;
                continue;
            }

            var severity = WeatherAssessment.Severity(observation, _options);
            if (IncidentConstants.Severities.Rank(severity) > IncidentConstants.Severities.Rank(worstSeverity))
            {
                worst = observation;
                worstSeverity = severity;
            }
        }

        return new RouteAssessment(worst, worstSeverity, observedAll, newFailures);
    }

    private async Task<WeatherObservation?> TryObserveAsync(double lat, double lng, CancellationToken cancellationToken)
    {
        try
        {
            return await weather.GetCurrentAsync(lat, lng, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Lỗi dịch vụ ngoài chỉ ghi log, không làm hỏng cả lượt quét
            logger.LogWarning(ex, "[WEATHER GUARD] Không lấy được thời tiết tại ({Lat}, {Lng}).", lat, lng);
            return null;
        }
    }

    /// <returns>true nếu tạo Incident mới, false nếu cập nhật Incident đang mở.</returns>
    private async Task<bool> RecordIncidentAsync(
        Guid routeId, string routeCode, WeatherObservation observation, string severity,
        List<Guid> affectedTripIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var windowStart = now.AddHours(-_options.AlertWindowHours);
        var description = WeatherAssessment.Describe(routeCode, observation);

        // Danh sách đã sắp mới nhất trước: phần tử đầu còn trong khung cảnh báo là Incident cần cập nhật
        var open = await incidents.ListOpenWeatherAsync(routeId, cancellationToken);
        var incident = open.FirstOrDefault(i => i.OccurredAt >= windowStart);

        var isNew = incident is null;
        var escalated = false;

        if (incident is null)
        {
            incident = new Incident
            {
                ScopeType = IncidentConstants.Scopes.Route,
                RouteId = routeId,
                Source = IncidentConstants.Sources.WeatherService,
                IncidentType = IncidentConstants.Types.Weather,
                Severity = severity,
                Status = IncidentConstants.Statuses.Open,
                Description = description,
                OccurredAt = now
            };
            incidents.Add(incident);

            // Cảnh báo cũ của tuyến (đã quá khung AlertWindowHours) được thay bằng cảnh báo mới này
            foreach (var old in open) CloseIncident(old, now);
        }
        else
        {
            escalated = IncidentConstants.Severities.Rank(severity) > IncidentConstants.Severities.Rank(incident.Severity);
            if (escalated) incident.Severity = severity;
            incident.Description = description;
            incident.UpdatedAt = now;
        }

        // Chỉ liên kết thêm Trip mới bị ảnh hưởng; quyết định vận hành để trống cho Admin
        var linked = incident.AffectedTrips.Select(t => t.TripId).ToHashSet();
        foreach (var tripId in affectedTripIds.Where(id => !linked.Contains(id)))
        {
            incidents.AddTripLink(new IncidentTrip { IncidentId = incident.Id, TripId = tripId });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (isNew || escalated)
        {
            await alerts.PublishWeatherAlertAsync(
                new WeatherAlertMessage(incident.Id, incident.Severity, routeId, description, now), cancellationToken);
        }

        return isNew;
    }

    /// <summary>Đóng các Incident thời tiết do Weather Guard tạo đang mở và thoả điều kiện.</summary>
    private async Task<int> ResolveOpenIncidentsAsync(
        Func<Incident, bool> filter, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var toClose = (await incidents.ListOpenWeatherAsync(cancellationToken: cancellationToken)).Where(filter).ToList();
        if (toClose.Count == 0) return 0;

        foreach (var incident in toClose) CloseIncident(incident, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return toClose.Count;
    }

    private static void CloseIncident(Incident incident, DateTimeOffset now)
    {
        incident.Status = IncidentConstants.Statuses.Resolved;
        incident.ResolvedAt = now;
        incident.UpdatedAt = now;
    }
}