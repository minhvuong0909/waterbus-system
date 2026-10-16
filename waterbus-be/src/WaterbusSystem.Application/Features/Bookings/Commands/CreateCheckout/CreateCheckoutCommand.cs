using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckout;

public record CheckoutLeg(Guid TripId, List<Guid> SeatIds, Guid BoardingStationId, Guid DisembarkingStationId,
    IReadOnlyList<PassengerInput>? Passengers = null, Guid? BoardingCallId = null, Guid? DisembarkingCallId = null);
public record CheckoutResponseDto(Guid OrderId, decimal QuotedTotal, string Currency, string Status,
    DateTimeOffset? ExpiresAt, string? AccessToken, IReadOnlyList<BookingResponseDto> Bookings);
public record CreateCheckoutCommand(string CustomerName, string CustomerEmail, string? CustomerPhone,
    List<CheckoutLeg> Legs) : IRequest<CheckoutResponseDto>
{
    public CreateBookingCommand ForLeg(CheckoutLeg leg) => new(leg.TripId, leg.SeatIds, leg.BoardingStationId,
        leg.DisembarkingStationId, CustomerName, CustomerEmail, CustomerPhone, leg.Passengers,
        leg.BoardingCallId, leg.DisembarkingCallId);
}
public sealed class CreateCheckoutCommandValidator : AbstractValidator<CreateCheckoutCommand>
{
    public CreateCheckoutCommandValidator()
    {
        RuleFor(x => x.Legs).Cascade(CascadeMode.Stop).NotNull().Must(x => x.Count is 1 or 2);
        RuleForEach(x => x.Legs).NotNull();
        RuleFor(x => x).Custom((command, context) =>
        {
            foreach (var leg in command.Legs ?? [])
            {
                if (leg is null) continue;
                foreach (var error in new CreateBookingCommandValidator().Validate(command.ForLeg(leg)).Errors)
                    context.AddFailure(error);
            }
        });
    }
}
public sealed class CreateCheckoutCommandHandler(IApplicationDbContext context,
    BookingCreationService creation, ICommercialTransactionRunner transactions,
    CheckoutCommitEffects effects) : IRequestHandler<CreateCheckoutCommand, CheckoutResponseDto>
{
    public async Task<CheckoutResponseDto> Handle(CreateCheckoutCommand request, CancellationToken ct)
    {
        var result = await transactions.RunAsync(async () =>
        {
            var first = await creation.CreateAsync(request.ForLeg(request.Legs[0]), ct);
            var order = await context.PurchaseOrders.FirstAsync(o => o.Id == first.OrderId, ct);
            var bookings = new List<BookingResponseDto> { first };
            if (request.Legs.Count == 2)
                bookings.Add(await creation.CreateAsync(request.ForLeg(request.Legs[1]), ct, order));
            return new CheckoutResponseDto(order.Id, order.QuotedTotal, order.Currency, order.Status.ToString(),
                order.ExpiresAt, first.AccessToken, bookings);
        }, ct);
        await effects.HeldAsync(result.OrderId, ct);
        return result;
    }
}
