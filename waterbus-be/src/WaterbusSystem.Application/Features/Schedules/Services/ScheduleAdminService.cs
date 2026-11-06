using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Schedules;
using WaterbusSystem.Application.DTOs.Responses.Schedules;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Validators.Schedules;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Schedules.Services;

/// <summary>
/// Quản lý Schedule (BR-SCH-01..06, BR-ROUTE-07, BR-STOP-01). Không tự sinh/viết lại Trip: đổi lịch chỉ ảnh hưởng lần sinh chuyến sau
/// (BR-SCH-06). Điểm dừng lịch phải đi theo thứ tự RouteStop (được bỏ bến = chạy một phần), có giờ đến/rời đầy đủ (BR-STOP-01).
/// </summary>
public sealed class ScheduleAdminService(
    IScheduleAdminRepository schedules, IUnitOfWork unitOfWork, ICommercialTransactionRunner transactions) : IScheduleAdminService
{
    public Task<PaginatedList<ScheduleListItemResponse>> GetPageAsync(GetSchedulesRequest request, CancellationToken cancellationToken = default) =>
        schedules.GetPageAsync(request, cancellationToken);

    public Task<ScheduleDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        schedules.GetDetailAsync(id, cancellationToken);

    public Task<Guid> CreateAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var route = await RequireUsableRouteAsync(request.RouteId, request.TripType, cancellationToken);
            ValidateStops(route, request.Stops, request.Direction);
            var days = ScheduleRules.ParseDays(request.DaysOfWeek)!;
            var schedule = new Schedule
            {
                RouteId = route.Id, TripType = Enum.Parse<TripType>(request.TripType), Direction = request.Direction,
                DepartureTime = request.DepartureTime, DaysOfWeek = string.Join(',', days),
                EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, IsActive = true
            };
            await EnsureNoConflictAsync(schedule, null, cancellationToken);
            schedules.Add(schedule);
            AddStops(schedule.Id, request.Stops);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return schedule.Id;
        }, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateScheduleRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var schedule = await schedules.GetTrackedWithStopsAsync(id, cancellationToken);
            var tripType = Enum.Parse<TripType>(request.TripType);
            if (tripType != schedule.TripType)
            {
                if (await schedules.HasTripsAsync(id, cancellationToken))
                    throw new ConflictException("Lịch đã sinh Trip nên không đổi được loại chuyến.");
                await RequireUsableRouteAsync(schedule.RouteId, request.TripType, cancellationToken);
            }
            if (request.Direction != schedule.Direction)
            {
                if (await schedules.HasTripsAsync(id, cancellationToken))
                    throw new ConflictException("Lịch đã sinh Trip nên không đổi được chiều chạy.");
                // Điểm dừng hiện có phải hợp lệ với chiều mới (Outbound: SequenceNo tăng; Inbound: giảm).
                var route = await schedules.GetRouteWithStopsAsync(schedule.RouteId, cancellationToken);
                ValidateStops(route, schedule.Stops.Where(s => !s.IsDeleted).OrderBy(s => s.VisitOrder)
                    .Select(s => new ScheduleStopInput(s.RouteStopId, s.ArrivalOffsetMin, s.DepartureOffsetMin)).ToList(), request.Direction);
            }
            schedule.TripType = tripType;
            schedule.Direction = request.Direction;
            schedule.DepartureTime = request.DepartureTime;
            schedule.DaysOfWeek = string.Join(',', ScheduleRules.ParseDays(request.DaysOfWeek)!);
            schedule.EffectiveFrom = request.EffectiveFrom;
            schedule.EffectiveTo = request.EffectiveTo;
            schedule.UpdatedAt = DateTimeOffset.UtcNow;
            if (schedule.IsActive) await EnsureNoConflictAsync(schedule, id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task<ScheduleDetailResponse> ReplaceStopsAsync(Guid id, ReplaceScheduleStopsRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var schedule = await schedules.GetTrackedWithStopsAsync(id, cancellationToken);
            if (await schedules.HasTripsAsync(id, cancellationToken))
                throw new ConflictException("Lịch đã sinh Trip nên không đổi được điểm dừng (BR-SCH-06). Hãy tạo lịch mới.");
            var route = await schedules.GetRouteWithStopsAsync(schedule.RouteId, cancellationToken);
            ValidateStops(route, request.Stops, schedule.Direction);

            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < request.Stops.Count; i++)
            {
                var input = request.Stops[i];
                var row = schedule.Stops.FirstOrDefault(s => s.VisitOrder == i + 1);
                if (row is null)
                {
                    schedules.AddStop(new ScheduleStop
                    {
                        ScheduleId = id, RouteStopId = input.RouteStopId, VisitOrder = i + 1,
                        ArrivalOffsetMin = input.ArrivalOffsetMin, DepartureOffsetMin = input.DepartureOffsetMin
                    });
                    continue;
                }
                row.RouteStopId = input.RouteStopId;
                row.ArrivalOffsetMin = input.ArrivalOffsetMin;
                row.DepartureOffsetMin = input.DepartureOffsetMin;
                row.IsDeleted = false;
                row.UpdatedAt = now;
            }
            foreach (var extra in schedule.Stops.Where(s => s.VisitOrder > request.Stops.Count && !s.IsDeleted))
            {
                extra.IsDeleted = true;
                extra.UpdatedAt = now;
            }
            schedule.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await schedules.GetDetailAsync(id, cancellationToken);
        }, cancellationToken);

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var schedule = await schedules.GetTrackedWithStopsAsync(id, cancellationToken);
            if (!schedule.IsActive) return true;
            schedule.IsActive = false; // Trip đã sinh giữ nguyên (BR-SCH-06)
            schedule.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task ActivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var schedule = await schedules.GetTrackedWithStopsAsync(id, cancellationToken);
            if (schedule.IsActive) return true;
            await RequireUsableRouteAsync(schedule.RouteId, schedule.TripType.ToString(), cancellationToken);
            await EnsureNoConflictAsync(schedule, id, cancellationToken);
            schedule.IsActive = true;
            schedule.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    // ---- helpers ----

    private async Task<Route> RequireUsableRouteAsync(Guid routeId, string tripType, CancellationToken cancellationToken)
    {
        var route = await schedules.GetRouteWithStopsAsync(routeId, cancellationToken);
        if (!route.IsActive) throw new ValidationException([new("routeId", "Route đã ngừng hoạt động.")]);
        var expected = tripType == nameof(TripType.Sightseeing) ? "Sightseeing" : "Regular";
        if (!string.Equals(route.ServiceType, expected, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException([new("tripType", $"Loại chuyến {tripType} không khớp loại dịch vụ của Route ({route.ServiceType}).")]);
        return route;
    }

    /// <summary>Outbound đi theo SequenceNo tăng dần, Inbound theo SequenceNo giảm dần (RouteStop.SequenceNo là chiều xuôi).</summary>
    private static void ValidateStops(Route route, List<ScheduleStopInput> stops, string direction)
    {
        var errors = new List<FluentValidation.Results.ValidationFailure>();
        var byId = route.Stops.Where(s => !s.IsDeleted).ToDictionary(s => s.Id);
        RouteStop? previous = null;
        for (var i = 0; i < stops.Count; i++)
        {
            if (!byId.TryGetValue(stops[i].RouteStopId, out var stop))
            {
                errors.Add(new($"stops[{i}].routeStopId", "Điểm dừng không thuộc Route của lịch."));
                continue;
            }
            var inbound = direction == "Inbound";
            if (previous != null && (inbound ? stop.SequenceNo >= previous.SequenceNo : stop.SequenceNo <= previous.SequenceNo))
                errors.Add(new($"stops[{i}].routeStopId",
                    inbound ? "Chiều Inbound phải đi theo SequenceNo giảm dần của Route." : "Chiều Outbound phải đi theo SequenceNo tăng dần của Route (không đảo, không lặp)."));
            previous = stop;

            var arrival = stops[i].ArrivalOffsetMin;
            var departure = stops[i].DepartureOffsetMin;
            if (i > 0 && arrival is null) errors.Add(new($"stops[{i}].arrivalOffsetMin", "Thiếu giờ đến dự kiến (BR-STOP-01)."));
            if (i < stops.Count - 1 && departure is null) errors.Add(new($"stops[{i}].departureOffsetMin", "Thiếu giờ rời dự kiến (BR-STOP-01)."));
            if (arrival is { } a && departure is { } d && a > d) errors.Add(new($"stops[{i}].departureOffsetMin", "Giờ rời không được sớm hơn giờ đến."));
            if (i > 0 && stops[i - 1].DepartureOffsetMin is { } prevDep && arrival is { } arr && arr < prevDep)
                errors.Add(new($"stops[{i}].arrivalOffsetMin", "Giờ đến không được sớm hơn giờ rời của điểm dừng trước."));
        }
        if (stops[0].DepartureOffsetMin is { } first && first != 0)
            errors.Add(new("stops[0].departureOffsetMin", "Điểm dừng đầu rời đúng giờ khởi hành (offset 0)."));
        if (byId.TryGetValue(stops[0].RouteStopId, out var head) && !head.CanBoard)
            errors.Add(new("stops[0].routeStopId", "Điểm dừng đầu phải cho khách lên tàu."));
        if (byId.TryGetValue(stops[^1].RouteStopId, out var tail) && !tail.CanAlight)
            errors.Add(new($"stops[{stops.Count - 1}].routeStopId", "Điểm dừng cuối phải cho khách xuống tàu."));
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private void AddStops(Guid scheduleId, List<ScheduleStopInput> stops)
    {
        for (var i = 0; i < stops.Count; i++)
            schedules.AddStop(new ScheduleStop
            {
                ScheduleId = scheduleId, RouteStopId = stops[i].RouteStopId, VisitOrder = i + 1,
                ArrivalOffsetMin = stops[i].ArrivalOffsetMin, DepartureOffsetMin = stops[i].DepartureOffsetMin
            });
    }

    /// <summary>Có ít nhất một ngày thật sự nằm trong cả hai khoảng hiệu lực và khớp thứ của cả hai lịch.</summary>
    private static bool SharesDepartureDate(Schedule candidate, SortedSet<int> candidateDays, Schedule other)
    {
        var from = candidate.EffectiveFrom > other.EffectiveFrom ? candidate.EffectiveFrom : other.EffectiveFrom;
        var to = Min(candidate.EffectiveTo, other.EffectiveTo);
        if (to is { } end && end < from) return false;
        var otherDays = ScheduleRules.ParseDays(other.DaysOfWeek) ?? [];
        // Thứ lặp lại mỗi 7 ngày nên chỉ cần duyệt tối đa 7 ngày đầu của phần giao.
        for (var i = 0; i < 7; i++)
        {
            var date = from.AddDays(i);
            if (to is { } last && date > last) break;
            var iso = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            if (candidateDays.Contains(iso) && otherDays.Contains(iso)) return true;
        }
        return false;

        static DateOnly? Min(DateOnly? a, DateOnly? b) => a is null ? b : b is null ? a : a < b ? a : b;
    }

    /// <summary>Hai lịch cùng Route/loại/hướng/giờ mà ngày chạy và khoảng hiệu lực giao nhau là mơ hồ (sinh trùng chuyến).</summary>
    private async Task EnsureNoConflictAsync(Schedule candidate, Guid? excludeId, CancellationToken cancellationToken)
    {
        var siblings = await schedules.ListActiveSiblingsAsync(
            candidate.RouteId, candidate.TripType, candidate.Direction, candidate.DepartureTime, excludeId, cancellationToken);
        var days = ScheduleRules.ParseDays(candidate.DaysOfWeek)!;
        foreach (var other in siblings)
        {
            if (SharesDepartureDate(candidate, days, other))
                throw new ConflictException("Đã có lịch hoạt động cùng tuyến, loại chuyến, hướng và giờ khởi hành trùng ngày/hiệu lực.");
        }
    }
}
