using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Bookings.Commands.UpdateCheckoutSeats;

public record UpdateCheckoutSeatsCommand(Guid OrderId, Guid BookingId, List<Guid> SeatIds,
    IReadOnlyList<PassengerInput> Passengers, string? AccessToken = null) : IRequest<decimal>;
public sealed class UpdateCheckoutSeatsCommandValidator : AbstractValidator<UpdateCheckoutSeatsCommand>
{
    public UpdateCheckoutSeatsCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty(); RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.SeatIds).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(ids => ids.Count <= 10 && ids.Distinct().Count() == ids.Count);
        RuleFor(x => x.Passengers).Cascade(CascadeMode.Stop).NotNull()
            .Must((c, passengers) => passengers.Count == c.SeatIds?.Count);
        RuleForEach(x => x.Passengers).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            p.RuleFor(x => x.Email).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
            p.RuleFor(x => x.Phone).MaximumLength(20);
        });
    }
}
public sealed class UpdateCheckoutSeatsCommandHandler(IApplicationDbContext context, ICommercialTransactionRunner transactions,
    CheckoutAccessService access, ISeatAvailabilityService availability, ICheckoutSessionService sessions,
    ISeatEventPublisher? events = null) : IRequestHandler<UpdateCheckoutSeatsCommand, decimal>
{
    public async Task<decimal> Handle(UpdateCheckoutSeatsCommand request, CancellationToken ct)
    {
        await access.RequireAsync(request.OrderId, request.AccessToken, ct);
        var result = await transactions.RunAsync(async () =>
        {
            var order = await context.PurchaseOrders.Include(o => o.Bookings).ThenInclude(b => b.SeatReservations)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct) ?? throw new NotFoundException(nameof(PurchaseOrder), request.OrderId);
            var now = DateTimeOffset.UtcNow;
            if (order.Status is not (OrderStatus.PendingPayment or OrderStatus.Draft) || order.ExpiresAt == null || order.ExpiresAt <= now ||
                await context.FinancialTransactions.AnyAsync(p => p.OrderId == order.Id, ct))
                throw new ConcurrencyException("Chỉ đổi ghế trước khi thanh toán và trong hạn checkout gốc.");
            var booking = order.Bookings.FirstOrDefault(b => b.Id == request.BookingId)
                ?? throw new NotFoundException(nameof(Booking), request.BookingId);
            var trip = await context.Trips.FirstAsync(t => t.Id == booking.TripId, ct);
            if (trip.Status is not (TripStatus.Scheduled or TripStatus.Boarding) || trip.SalesCloseAt <= now || trip.ActualDepartureTime != null)
                throw new ConcurrencyException("Chuyến không còn mở bán.");
            var seats = await context.Seats.Include(s => s.SeatClass).Where(s => request.SeatIds.Contains(s.Id) && s.IsActive && s.BoatId == trip.BoatId).ToListAsync(ct);
            if (seats.Count != request.SeatIds.Count) throw new ConcurrencyException("Ghế không hợp lệ cho tàu này.");
            var old = booking.SeatReservations.Where(r => r.Status == ReservationStatus.Pending).ToList();
            foreach (var r in old) r.Status = ReservationStatus.Cancelled;
            await context.SaveChangesAsync(ct);
            if (old.Count == 0) throw new ConcurrencyException("Checkout không còn ghế đang giữ.");
            var from = old[0].BoardingStopOrder; var to = old[0].DisembarkingStopOrder;
            if ((await availability.GetAvailableSeatsAsync(trip.Id, request.SeatIds, from, to, ct)).Count != seats.Count)
                throw new ConcurrencyException("Ghế đã được giữ/bán cho đoạn này.");
            var fares = await context.FareRules.AsNoTracking().Where(f => f.TripType == trip.TripType.ToString()
                && f.IsActive && f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo > now)).ToListAsync(ct);
            if (fares.GroupBy(f => f.SeatClassId).Any(g => g.Count() > 1))
                throw new ConcurrencyException("Có nhiều bảng giá đang hiệu lực cho cùng hạng ghế.");
            var added = new List<SeatReservation>();
            for (var i = 0; i < request.SeatIds.Count; i++)
            {
                var seat = seats.Single(s => s.Id == request.SeatIds[i]);
                var fare = fares.SingleOrDefault(f => f.SeatClassId == seat.SeatClassId)
                    ?? throw new ConcurrencyException("Chưa có giá hợp lệ cho ghế.");
                var passenger = request.Passengers[i];
                var r = new SeatReservation { Booking = booking, BookingId = booking.Id, TripId = trip.Id,
                    SeatId = seat.Id, BoardingStopOrder = from, DisembarkingStopOrder = to,
                    QuotedFare = fare.Price, PassengerName = passenger.Name, PassengerEmail = passenger.Email,
                    PassengerPhone = passenger.Phone, SeatCodeAtSale = seat.SeatCode, SeatClassAtSale = seat.SeatClass!.Name };
                context.SeatReservations.Add(r); added.Add(r);
            }
            booking.QuotedTotal = added.Sum(r => r.QuotedFare);
            order.QuotedTotal = order.Bookings.Sum(b => b.QuotedTotal);
            await context.SaveChangesAsync(ct);
            return (order, old, added);
        }, ct);
        // Không đặt lại expiry từ now: cache chỉ phản ánh hạn DB ban đầu.
        try
        {
            await sessions.ReleaseAsync("order:" + request.OrderId, ct);
            await sessions.HoldUntilAsync("order:" + request.OrderId, request.OrderId,
                result.order.Bookings.SelectMany(b => b.SeatReservations).Where(r => r.Status == ReservationStatus.Pending)
                    .Select(r => new SeatHold(r.TripId, r.SeatId, r.BoardingStopOrder, r.DisembarkingStopOrder)).ToList(), result.order.ExpiresAt!.Value, ct);
        }
        catch (Exception) { }
        await CheckoutSeatEvents.PublishAsync(events, result.old, "Released", null, ct);
        await CheckoutSeatEvents.PublishAsync(events, result.added, "Held", result.order.ExpiresAt, ct);
        return result.order.QuotedTotal;
    }
}
