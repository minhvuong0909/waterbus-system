using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.TripPlanning;
using WaterbusSystem.Application.DTOs.Responses.TripPlanning;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.TripPlanning.Services;

/// <summary>
/// Sinh Trip từ Schedule và gán/đổi tàu (BR-SCH-01..04, 07, 08, BR-STOP-01). Không có vòng đời Trip mới: Trip sinh ra ở Scheduled,
/// chưa có tàu (chưa bán được, BR-SCH-04); mọi đổi trạng thái vẫn qua TripService. Giờ lịch là giờ Việt Nam (UTC+7) như dữ liệu seed.
/// </summary>
public sealed class TripPlanningService(
    ITripPlanningRepository trips, IUnitOfWork unitOfWork, ICommercialTransactionRunner transactions, IDateTimeService dateTime)
    : ITripPlanningService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public Task<GenerateTripsResponse> GenerateFromScheduleAsync(GenerateTripsRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var schedule = await trips.GetScheduleForGenerationAsync(request.ScheduleId, cancellationToken);
            if (!schedule.IsActive) throw new ConflictException("Lịch đã ngừng hoạt động nên không sinh chuyến.");
            var route = schedule.Route!;
            if (!route.IsActive) throw new ConflictException("Route của lịch đã ngừng hoạt động nên không sinh chuyến.");
            var expected = schedule.TripType == TripType.Sightseeing ? "Sightseeing" : "Regular";
            if (!string.Equals(route.ServiceType, expected, StringComparison.OrdinalIgnoreCase))
                throw new ConflictException("Loại chuyến của lịch không còn khớp loại dịch vụ của Route.");

            var stops = schedule.Stops.Where(s => !s.IsDeleted && s.RouteStop is { IsDeleted: false }).OrderBy(s => s.VisitOrder).ToList();
            if (stops.Count < 2 || stops[^1].ArrivalOffsetMin is not { } totalMinutes || totalMinutes <= 0
                || stops.Take(stops.Count - 1).Any(s => s.DepartureOffsetMin is null) || stops.Skip(1).Any(s => s.ArrivalOffsetMin is null))
                throw new ConflictException("Lịch thiếu điểm dừng hoặc giờ đến/rời dự kiến nên không sinh chuyến được (BR-STOP-01).");

            var days = schedule.DaysOfWeek.Split(',').Select(d => int.Parse(d.Trim())).ToHashSet();
            var firstUtc = Departure(request.FromDate, schedule.DepartureTime);
            var existing = await trips.ListExistingDeparturesAsync(
                schedule.Id, firstUtc, Departure(request.ToDate, schedule.DepartureTime), cancellationToken);
            var now = dateTime.Now;

            var created = new List<GeneratedTripResponse>();
            int skippedExisting = 0, skippedPast = 0, skippedDay = 0;
            for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
            {
                var isoDay = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
                if (!days.Contains(isoDay) || date < schedule.EffectiveFrom || (schedule.EffectiveTo is { } to && date > to)) { skippedDay++; continue; }
                var departure = Departure(date, schedule.DepartureTime);
                if (departure <= now) { skippedPast++; continue; }
                if (existing.Contains(departure)) { skippedExisting++; continue; }

                var trip = new Trip
                {
                    ScheduleId = schedule.Id, RouteId = route.Id, TripType = schedule.TripType, Status = TripStatus.Scheduled,
                    DepartureTime = departure, ArrivalTime = departure.AddMinutes(totalMinutes),
                    // BR-TIME-01/02: thiếu cấu hình thì trần bắt buộc là giờ khởi hành (không bán sau khi Trip rời bến);
                    // Admin vẫn có thể cấu hình sớm hơn. Không đặt null vì null = không có mốc đóng bán.
                    SalesCloseAt = departure.AddMinutes(-(request.SalesCloseMinutesBeforeDeparture ?? 0))
                };
                foreach (var stop in stops)
                {
                    var stopDeparture = stop.DepartureOffsetMin is { } d ? departure.AddMinutes(d) : (DateTimeOffset?)null;
                    trip.TripStopCalls.Add(new TripStopCall
                    {
                        TripId = trip.Id, RouteStopId = stop.RouteStopId, ScheduleStopId = stop.Id, VisitOrder = stop.VisitOrder,
                        EstimatedArrivalTime = stop.ArrivalOffsetMin is { } a ? departure.AddMinutes(a) : null,
                        EstimatedDepartureTime = stopDeparture,
                        CheckInOpenAt = request.CheckInOpenMinutesBeforeDeparture is { } lead ? (stopDeparture ?? departure).AddMinutes(-lead) : null,
                        CheckInCloseAt = request.CheckInOpenMinutesBeforeDeparture is not null ? stopDeparture ?? departure : null,
                        Status = "Planned"
                    });
                }
                trips.AddTrip(trip);
                created.Add(new(trip.Id, trip.DepartureTime, trip.ArrivalTime));
            }

            if (created.Count > 0) await unitOfWork.SaveChangesAsync(cancellationToken);
            return new GenerateTripsResponse(schedule.Id, created, skippedExisting, skippedPast, skippedDay);
        }, cancellationToken);

    public Task<AssignTripBoatResponse> AssignBoatAsync(Guid tripId, AssignTripBoatRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var trip = await trips.GetTripTrackedAsync(tripId, cancellationToken);
            if (trip.Status != TripStatus.Scheduled)
                throw new ConflictException($"Chỉ đổi/gán tàu khi chuyến còn ở trạng thái Scheduled (hiện {trip.Status}).");
            if (trip.BoatId == request.BoatId) return new AssignTripBoatResponse(trip.Id, request.BoatId, trip.BoatId, 0);

            var boat = await trips.GetBoatWithActiveSeatsAsync(request.BoatId, cancellationToken);
            if (!boat.IsActive) throw new ValidationException([new("boatId", "Tàu đã ngừng hoạt động.")]);
            var seats = boat.Seats.Where(s => s.IsActive && !s.IsDeleted && s.SeatClass is { IsActive: true, IsDeleted: false }).ToList();
            if (seats.Count == 0) throw new ValidationException([new("boatId", "Tàu chưa có ghế hợp lệ (BR-SCH-04).")]);
            if (await trips.HasOverlappingTripAsync(boat.Id, trip.DepartureTime, trip.ArrivalTime, trip.Id, cancellationToken))
                throw new ConflictException("Tàu đã được gán cho chuyến khác chồng thời gian vận hành (BR-SCH-08).");

            // BR-SCH-07: ghế đã bán phải có ghế cùng mã + cùng hạng trên tàu mới; remap nhất quán, không tự đổi hạng/ghế.
            var reservations = await trips.ListLiveReservationsTrackedAsync(trip.Id, cancellationToken);
            var byCode = seats.ToDictionary(s => s.SeatCode, StringComparer.OrdinalIgnoreCase);
            var incompatible = reservations
                .Where(r => !byCode.TryGetValue(r.Seat!.SeatCode, out var target) || target.SeatClassId != r.Seat.SeatClassId)
                .Select(r => r.Seat!.SeatCode).Distinct().ToList();
            if (incompatible.Count > 0)
                throw new ConflictException($"Tàu thay thế thiếu ghế tương thích với ghế đã bán: {string.Join(", ", incompatible)} (BR-SCH-07).");

            var now = DateTimeOffset.UtcNow;
            foreach (var reservation in reservations)
            {
                reservation.SeatId = byCode[reservation.Seat!.SeatCode].Id;
                reservation.UpdatedAt = now;
            }
            var previous = trip.BoatId;
            trip.BoatId = boat.Id;
            trip.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new AssignTripBoatResponse(trip.Id, boat.Id, previous, reservations.Count);
        }, cancellationToken);

    private static DateTimeOffset Departure(DateOnly date, TimeSpan timeOfDay) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue) + timeOfDay, VietnamOffset).ToUniversalTime();
}
