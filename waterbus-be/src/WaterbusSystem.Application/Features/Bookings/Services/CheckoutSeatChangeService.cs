
using FluentValidation;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException =
    WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class CheckoutSeatChangeService(
    IOrderRepository orders,
    IBookingReferenceRepository references,
    IBookingRepository bookings,
    ISeatAvailabilityService availability,
    ICheckoutAccessService access,
    ICommercialTransactionRunner transactions,
    IUnitOfWork unitOfWork,
    CheckoutCommitEffects effects,
    IValidator<UpdateCheckoutSeatsRequest> validator)
    : ICheckoutSeatChangeService
{
    public async Task<decimal> UpdateAsync(
        Guid orderId,
        Guid bookingId,
        UpdateCheckoutSeatsRequest request,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(
            request, cancellationToken);

        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        await access.RequireAsync(
            orderId, accessToken, cancellationToken);

        var result = await transactions.RunAsync(async () =>
        {
            var order = await orders.GetForSubmissionAsync(
                orderId, cancellationToken);

            var now = DateTimeOffset.UtcNow;

            if (order.Status is not (
                    OrderStatus.Draft or OrderStatus.PendingPayment) ||
                order.ExpiresAt is null ||
                order.ExpiresAt <= now ||
                await orders.HasFinancialTransactionsAsync(
                    orderId, cancellationToken))
            {
                throw new ConcurrencyException(
                    "Chỉ đổi ghế trước khi thanh toán và trong hạn checkout gốc.");
            }

            var booking = order.Bookings.FirstOrDefault(
                b => b.Id == bookingId)
                ?? throw new NotFoundException(
                    nameof(Booking), bookingId);

            var trip = await references.GetTripWithStopsAsync(
                booking.TripId, cancellationToken)
                ?? throw new NotFoundException(
                    nameof(Trip), booking.TripId);

            if (trip.Boat is not { IsActive: true, IsDeleted: false } || trip.Status is not (
                    TripStatus.Scheduled or TripStatus.Boarding) ||
                (trip.SalesCloseAt is { } salesCloseAt &&
                 salesCloseAt <= now) ||
                trip.ActualDepartureTime is not null)
            {
                throw new ConcurrencyException(
                    "Chuyến không còn mở bán.");
            }

            var seats = await references.GetActiveSeatsAsync(
                request.SeatIds, cancellationToken);

            if (trip.BoatId is null ||
                seats.Count != request.SeatIds.Count ||
                seats.Any(s => s.BoatId != trip.BoatId || s.SeatClass is not { IsActive: true, IsDeleted: false }))
            {
                throw new ConcurrencyException(
                    "Ghế không hợp lệ cho tàu này.");
            }

            var fares = await references.GetEffectiveFaresAsync(
                trip.TripType, now, cancellationToken);

            if (fares.GroupBy(f => f.SeatClassId).Any(g => g.Count() > 1))
                throw new ConcurrencyException("Có nhiều bảng giá đang hiệu lực cho cùng hạng ghế.");

            var seatById = seats.ToDictionary(s => s.Id);
            var fareByClass = fares.ToDictionary(f => f.SeatClassId);
            if (seats.Any(s => !fareByClass.TryGetValue(s.SeatClassId, out var fare) ||
                fare.Currency != "VND" || fare.Price < 0))
                throw new ConcurrencyException("Chưa có giá vé không âm bằng VND hợp lệ cho ghế.");

            var old = booking.SeatReservations
                .Where(r => r.Status == ReservationStatus.Pending)
                .ToList();

            if (old.Count == 0)
                throw new ConcurrencyException(
                    "Checkout không còn ghế đang giữ.");

            var from = old[0].BoardingStopOrder;
            var to = old[0].DisembarkingStopOrder;

            // Thao tác này nằm trong SQL transaction.
            foreach (var reservation in old)
                reservation.Status = ReservationStatus.Cancelled;

            await unitOfWork.SaveChangesAsync(cancellationToken);

            var available = await availability.GetAvailableSeatsAsync(
                trip.Id,
                request.SeatIds,
                from,
                to,
                cancellationToken);

            if (available.Count != seats.Count)
                throw new ConcurrencyException(
                    "Ghế đã được giữ hoặc bán trên đoạn chặng này.");

            var added = new List<SeatReservation>();

            for (var i = 0; i < request.SeatIds.Count; i++)
            {
                var seat = seatById[request.SeatIds[i]];

                var fare = fareByClass[seat.SeatClassId];

                var passenger = request.Passengers[i];

                var reservation = new SeatReservation
                {
                    Booking = booking,
                    BookingId = booking.Id,
                    TripId = trip.Id,
                    SeatId = seat.Id,
                    BoardingStopOrder = from,
                    DisembarkingStopOrder = to,
                    Status = ReservationStatus.Pending,
                    QuotedFare = fare.Price,
                    PassengerName = passenger.Name,
                    PassengerEmail = passenger.Email,
                    PassengerPhone = passenger.Phone,
                    SeatCodeAtSale = seat.SeatCode,
                    SeatClassAtSale = seat.SeatClass!.Name
                };

                bookings.AddSeatReservation(reservation);
                added.Add(reservation);
            }

            booking.QuotedTotal = added.Sum(r => r.QuotedFare);
            order.QuotedTotal = order.Bookings.Sum(
                b => b.QuotedTotal);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return (
                Total: order.QuotedTotal,
                Old: old,
                Added: added
            );
        }, cancellationToken);

        // SQL đã commit; cập nhật Redis và realtime.
        await effects.SeatsUpdatedAsync(
            orderId,
            result.Old,
            result.Added,
            cancellationToken);

        return result.Total;
    }
}
