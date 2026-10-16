using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckout;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckoutHold;

/// <summary>Giữ ghế trước khi nhập người mua; Draft không phát vé hoặc cho thanh toán.</summary>
public record CreateCheckoutHoldCommand(List<CheckoutLeg> Legs) : IRequest<CheckoutResponseDto>;
public sealed class CreateCheckoutHoldCommandValidator : AbstractValidator<CreateCheckoutHoldCommand>
{
    public CreateCheckoutHoldCommandValidator()
    {
        RuleFor(x => x.Legs).Cascade(CascadeMode.Stop).NotNull().Must(legs => legs.Count is 1 or 2);
        RuleForEach(x => x.Legs).NotNull();
        RuleFor(x => x).Custom((command, context) =>
        {
            foreach (var leg in command.Legs ?? [])
            {
                if (leg is null) continue;
                var request = new CreateBookingCommand(leg.TripId, leg.SeatIds, leg.BoardingStationId,
                    leg.DisembarkingStationId, "Draft", "draft@example.invalid", null,
                    leg.Passengers, leg.BoardingCallId, leg.DisembarkingCallId);
                foreach (var error in new CreateBookingCommandValidator().Validate(request).Errors) context.AddFailure(error);
            }
        });
    }
}
public sealed class CreateCheckoutHoldCommandHandler(IApplicationDbContext context, BookingCreationService creation,
    ICommercialTransactionRunner transactions, CheckoutCommitEffects effects) : IRequestHandler<CreateCheckoutHoldCommand, CheckoutResponseDto>
{
    public async Task<CheckoutResponseDto> Handle(CreateCheckoutHoldCommand request, CancellationToken ct)
    {
        var result = await transactions.RunAsync(async () =>
        {
            var commands = request.Legs.Select(l => new CreateBookingCommand(l.TripId, l.SeatIds, l.BoardingStationId,
                l.DisembarkingStationId, "", "", null, l.Passengers, l.BoardingCallId, l.DisembarkingCallId)).ToList();
            var first = await creation.CreateAsync(commands[0], ct, draft: true);
            var order = await context.PurchaseOrders.FirstAsync(o => o.Id == first.OrderId, ct);
            var bookings = new List<BookingResponseDto> { first };
            if (commands.Count == 2) bookings.Add(await creation.CreateAsync(commands[1], ct, order, draft: true));
            return new CheckoutResponseDto(order.Id, order.QuotedTotal, order.Currency, order.Status.ToString(),
                order.ExpiresAt, first.AccessToken, bookings);
        }, ct);
        await effects.HeldAsync(result.OrderId, ct);
        return result;
    }
}
