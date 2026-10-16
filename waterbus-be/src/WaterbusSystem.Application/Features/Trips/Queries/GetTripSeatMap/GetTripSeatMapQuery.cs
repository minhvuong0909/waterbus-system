using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.Trips.Queries.GetTripDetail;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Trips.Queries.GetTripSeatMap;

public record TripSeatDto(Guid SeatId, string SeatCode, int RowNumber, int ColumnNumber,
    Guid SeatClassId, string? SeatClassName, decimal? Price, string Currency, string Status,
    string? UnavailableReason, DateTimeOffset? HoldExpiresAt);
public record TripSeatMapDto(Guid TripId, Guid BoatId, Guid BoardingCallId, Guid DisembarkingCallId,
    int BoardingVisitOrder, int DisembarkingVisitOrder, bool CanBook, string SalesState,
    int AvailableSeatsCount, DateTimeOffset AsOf, IReadOnlyList<TripSeatDto> Seats);
public record GetTripSeatMapQuery(Guid TripId, Guid BoardingCallId, Guid DisembarkingCallId) : IRequest<TripSeatMapDto>;

public sealed class GetTripSeatMapQueryValidator : AbstractValidator<GetTripSeatMapQuery>
{
    public GetTripSeatMapQueryValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.BoardingCallId).NotEmpty();
        RuleFor(x => x.DisembarkingCallId).NotEmpty().NotEqual(x => x.BoardingCallId);
    }
}

public sealed class GetTripSeatMapQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetTripSeatMapQuery, TripSeatMapDto>
{
    public async Task<TripSeatMapDto> Handle(GetTripSeatMapQuery request, CancellationToken ct)
    {
        var trip = await TripReadRules.LoadAsync(context, request.TripId, ct);
        var boarding = trip.TripStopCalls.SingleOrDefault(c => c.Id == request.BoardingCallId);
        var disembarking = trip.TripStopCalls.SingleOrDefault(c => c.Id == request.DisembarkingCallId);
        if (boarding is null || disembarking is null)
            throw new ValidationException([new("boardingCallId", "Điểm lên/xuống phải thuộc hành trình thực sự của chuyến.")]);
        // Dùng cùng quy tắc chọn hành trình với checkout, gồm chuyến vòng cùng Station.
        BookingCreationService.ResolveJourneyCalls(trip, boarding.RouteStop!.StationId,
            disembarking.RouteStop!.StationId, boarding.Id, disembarking.Id);
        if (trip.Boat is null) throw new ConcurrencyException("Chuyến chưa có tàu và sơ đồ ghế hợp lệ.");

        var now = clock.Now;
        var salesState = TripReadRules.SalesState(trip, now);
        var seats = await context.Seats.AsNoTracking().Include(s => s.SeatClass)
            .Where(s => s.BoatId == trip.Boat.Id).OrderBy(s => s.RowNumber).ThenBy(s => s.ColumnNumber)
            .ThenBy(s => s.SeatCode).ToListAsync(ct);
        var type = trip.TripType.ToString();
        var fares = await context.FareRules.AsNoTracking().Where(f => f.TripType == type && f.IsActive
            && f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo > now)).ToListAsync(ct);
        if (fares.GroupBy(f => f.SeatClassId).Any(g => g.Count() > 1))
            throw new ConcurrencyException("Có nhiều bảng giá cùng hiệu lực; cần Admin kiểm tra trước khi bán.");
        var priceByClass = fares.ToDictionary(f => f.SeatClassId);
        var overlaps = await context.SeatReservations.AsNoTracking()
            .Where(r => r.TripId == trip.Id && r.Status != ReservationStatus.Cancelled
                && boarding.VisitOrder < r.DisembarkingStopOrder && disembarking.VisitOrder > r.BoardingStopOrder)
            .Select(r => new { r.SeatId, r.Status, ExpiresAt = r.Booking!.Order!.ExpiresAt }).ToListAsync(ct);
        var bySeat = overlaps.ToLookup(r => r.SeatId);

        var result = seats.Select(seat =>
        {
            priceByClass.TryGetValue(seat.SeatClassId, out var fare);
            var reservations = bySeat[seat.Id].ToList();
            var reason = salesState != "open" ? salesState
                : !seat.IsActive || seat.SeatClass is not { IsActive: true, IsDeleted: false } ? "seatInactive"
                : fare is null ? "missingFare" : fare.Currency != "VND" ? "unsupportedCurrency" : null;
            var status = reason != null ? "unavailable"
                : reservations.Any(r => r.Status != ReservationStatus.Pending) ? "sold"
                : reservations.Count > 0 ? "held" : "available";
            return new TripSeatDto(seat.Id, seat.SeatCode, seat.RowNumber, seat.ColumnNumber,
                seat.SeatClassId, seat.SeatClass?.Name, fare?.Price, fare?.Currency ?? "VND", status, reason,
                status == "held" ? reservations.Max(r => r.ExpiresAt) : null);
        }).ToList();
        return new(trip.Id, trip.Boat.Id, boarding.Id, disembarking.Id, boarding.VisitOrder,
            disembarking.VisitOrder, salesState == "open" && result.Any(s => s.Status == "available"),
            salesState, result.Count(s => s.Status == "available"), now, result);
    }
}
