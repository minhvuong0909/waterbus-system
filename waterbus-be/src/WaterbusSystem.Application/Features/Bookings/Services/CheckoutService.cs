using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Domain.Entities;

using FluentValidation;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class CheckoutService(
    IOrderRepository orderRepository,
    BookingCreationService creation,
    ICommercialTransactionRunner transactions,
    CheckoutCommitEffects effects,
    CheckoutAccessService access,
    IUnitOfWork unitOfWork,
    IValidator<SubmitCheckoutRequest> submitValidator,
    IValidator<CreateCheckoutHoldRequest> validator,
    IValidator<CreateCheckoutRequest> createValidator,
    ICheckoutSeatChangeService seatChanges) : ICheckoutService
{
    public async Task<CheckoutResponse> HoldAsync(
        CreateCheckoutHoldRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var result = await transactions.RunAsync(async () =>
        {
            // Tạo Booking cho lượt đầu tiên
            var firstBooking = await creation.CreateAsync(
                ToCommand(request.Legs[0]),
                cancellationToken,
                draft: true);

            var order = await orderRepository.GetRequiredByIdAsync(firstBooking.OrderId, cancellationToken);

            var bookings = new List<BookingResponse>
            {
                ToResponse(firstBooking)
            };

            // Nếu khứ hồi, tạo Booking thứ hai trong cùng Order
            if (request.Legs.Count == 2)
            {
                var secondBooking = await creation.CreateAsync(
                    ToCommand(request.Legs[1]),
                    cancellationToken,
                    order,
                    draft: true);

                bookings.Add(ToResponse(secondBooking));
            }

            return new CheckoutResponse(
                order.Id,
                order.QuotedTotal,
                order.Currency,
                order.Status.ToString(),
                order.ExpiresAt,
                firstBooking.AccessToken,
                bookings);
        }, cancellationToken);

        // Chỉ đồng bộ Redis và realtime sau khi SQL commit
        await effects.HeldAsync(result.OrderId, cancellationToken);

        return result;
    }


    public async Task<bool> SubmitAsync(
        Guid orderId,
        SubmitCheckoutRequest request,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var validation = await submitValidator.ValidateAsync(
            request, cancellationToken);

        if (!validation.IsValid)
            throw new WaterbusSystem.Application.Common.Exceptions
                .ValidationException(validation.Errors);

        await access.RequireAsync(
            orderId, accessToken, cancellationToken);

        return await transactions.RunAsync(async () =>
        {
            var order = await orderRepository.GetForSubmissionAsync(
                orderId, cancellationToken);

            if (order.Status != OrderStatus.Draft ||
                order.ExpiresAt is null ||
                order.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                throw new ConcurrencyException(
                    "Checkout không còn là Draft hợp lệ.");
            }

            var holds = order.Bookings
                .SelectMany(b => b.SeatReservations)
                .Where(r => r.Status == ReservationStatus.Pending)
                .ToList();

            var passengers = request.Passengers
                ?? Array.Empty<CheckoutPassengerRequest>();

            if (passengers.Count > 0 &&
                (passengers.Count != holds.Count ||
                 passengers.Select(p => (p.BookingId, p.SeatId))
                     .Distinct().Count() != holds.Count ||
                 passengers.Any(p => !holds.Any(r =>
                     r.BookingId == p.BookingId &&
                     r.SeatId == p.SeatId))))
            {
                throw new ConcurrencyException(
                    "Hành khách phải khớp đúng Booking và ghế đang giữ.");
            }

            order.PurchaserName = request.CustomerName;
            order.PurchaserEmail = request.CustomerEmail;
            order.PurchaserPhone = request.CustomerPhone;

            foreach (var booking in order.Bookings)
            {
                booking.CustomerName = request.CustomerName;
                booking.CustomerEmail = request.CustomerEmail;
                booking.CustomerPhone = request.CustomerPhone;
                booking.Status = BookingStatus.PendingPayment;
            }

            foreach (var reservation in holds)
            {
                var passenger = passengers.FirstOrDefault(p =>
                    p.BookingId == reservation.BookingId &&
                    p.SeatId == reservation.SeatId);

                reservation.PassengerName =
                    passenger?.Name ?? request.CustomerName;
                reservation.PassengerEmail = passenger?.Email;
                reservation.PassengerPhone = passenger?.Phone;
            }

            order.ValidateForCheckout();
            order.Status = OrderStatus.PendingPayment;

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }


    public async Task<CheckoutResponse> CreateAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await createValidator.ValidateAsync(
            request, cancellationToken);

        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var result = await transactions.RunAsync(async () =>
        {
            var first = await creation.CreateAsync(
                ToBookingInput(request.Legs[0], request),
                cancellationToken);

            var order = await orderRepository.GetRequiredByIdAsync(
                first.OrderId, cancellationToken);

            var bookings = new List<BookingResponse>
            {
            ToResponse(first)
            };

            if (request.Legs.Count == 2)
            {
                var second = await creation.CreateAsync(
                    ToBookingInput(request.Legs[1], request),
                    cancellationToken,
                    order);

                bookings.Add(ToResponse(second));
            }

            return new CheckoutResponse(
                order.Id,
                order.QuotedTotal,
                order.Currency,
                order.Status.ToString(),
                order.ExpiresAt,
                first.AccessToken,
                bookings);
        }, cancellationToken);

        await effects.HeldAsync(
            result.OrderId, cancellationToken);

        return result;
    }


    public async Task<bool> CancelAsync(
        Guid orderId,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(
            orderId, accessToken, cancellationToken);

        var released = await transactions.RunAsync(async () =>
        {
            var order = await orderRepository.GetForSubmissionAsync(
                orderId, cancellationToken);

            if (order.Status == OrderStatus.Confirmed)
            {
                throw new ConcurrencyException(
                    "Đơn đã thanh toán không thể bỏ giữ ghế.");
            }

            var holds = order.Bookings
                .SelectMany(b => b.SeatReservations)
                .Where(r => r.Status == ReservationStatus.Pending)
                .ToList();

            order.Status = OrderStatus.Cancelled;

            foreach (var booking in order.Bookings.Where(b =>
                b.Status is BookingStatus.PendingPayment
                    or BookingStatus.Draft))
            {
                booking.Status = BookingStatus.Cancelled;
            }

            foreach (var reservation in holds)
            {
                reservation.Status = ReservationStatus.Cancelled;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return holds;
        }, cancellationToken);

        // Redis và realtime chỉ cập nhật sau khi SQL commit.
        await effects.ReleasedAsync(
            orderId, released, cancellationToken);

        return true;
    }

    public Task<decimal> UpdateSeatsAsync(
        Guid orderId,
        Guid bookingId,
        UpdateCheckoutSeatsRequest request,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        return seatChanges.UpdateAsync(
            orderId,
            bookingId,
            request,
            accessToken,
            cancellationToken);
    }

    private static BookingCreationInput ToBookingInput(
        CreateCheckoutLegRequest leg,
        CreateCheckoutRequest request)
    {
        return new BookingCreationInput(
            leg.TripId,
            leg.SeatIds,
            leg.BoardingStationId,
            leg.DisembarkingStationId,
            request.CustomerName,
            request.CustomerEmail,
            request.CustomerPhone,
            leg.Passengers?
                .Select(p => new BookingPassengerInput(
                    p.Name,
                    p.Email,
                    p.Phone))
                .ToList(),
            leg.BoardingCallId,
            leg.DisembarkingCallId);
    }



    private static CreateBookingCommand ToCommand(
        CheckoutHoldLegRequest leg) => new(
            leg.TripId,
            leg.SeatIds,
            leg.BoardingStationId,
            leg.DisembarkingStationId,
            "",
            "",
            null,
            null,
            leg.BoardingCallId,
            leg.DisembarkingCallId);

    private static BookingResponse ToResponse(
        BookingResponseDto booking) => new(
            booking.BookingId,
            booking.BookingCode,
            booking.TotalAmount,
            booking.Status,
            booking.HoldDurationSeconds,
            booking.OrderId,
            booking.OrderStatus,
            booking.BoardingCallId,
            booking.DisembarkingCallId,
            booking.BoardingStopOrder,
            booking.DisembarkingStopOrder,
            booking.CheckoutSessionId,
            booking.AccessToken);
}
