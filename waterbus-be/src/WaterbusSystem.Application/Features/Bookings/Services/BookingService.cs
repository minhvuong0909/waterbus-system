using FluentValidation;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Interfaces.Services;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class BookingService(BookingCreationService creation, ICommercialTransactionRunner transactions,
    CheckoutCommitEffects effects, IValidator<CreateBookingRequest> validator) : IBookingService
{
    public async Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);
        var input = new BookingCreationInput(request.TripId, request.SeatIds, request.BoardingStationId,
            request.DisembarkingStationId, request.CustomerName, request.CustomerEmail, request.CustomerPhone,
            request.Passengers?.Select(p => new BookingPassengerInput(p.Name, p.Email, p.Phone)).ToList(),
            request.BoardingCallId, request.DisembarkingCallId);
        var result = await transactions.RunAsync(() => creation.CreateAsync(input, ct), ct);
        await effects.HeldAsync(result.OrderId, ct);
        return result;
    }
}
