using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Bookings.Commands.SubmitCheckout;

public record CheckoutPassenger(Guid BookingId, Guid SeatId, string Name, string? Email, string? Phone);
public record SubmitCheckoutCommand(Guid OrderId, string CustomerName, string CustomerEmail, string? CustomerPhone,
    IReadOnlyList<CheckoutPassenger>? Passengers = null, string? AccessToken = null) : IRequest<bool>;
public sealed class SubmitCheckoutCommandValidator : AbstractValidator<SubmitCheckoutCommand>
{
    public SubmitCheckoutCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty(); RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.CustomerPhone).Matches(@"^[0-9]{10,11}$").When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone));
        RuleForEach(x => x.Passengers).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            p.RuleFor(x => x.Email).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
            p.RuleFor(x => x.Phone).MaximumLength(20);
        });
    }
}
public sealed class SubmitCheckoutCommandHandler(IApplicationDbContext context, CheckoutAccessService access,
    ICommercialTransactionRunner transactions) : IRequestHandler<SubmitCheckoutCommand, bool>
{
    public async Task<bool> Handle(SubmitCheckoutCommand request, CancellationToken ct)
    {
        await access.RequireAsync(request.OrderId, request.AccessToken, ct);
        return await transactions.RunAsync(async () =>
        {
            var order = await context.PurchaseOrders.Include(o => o.Bookings).ThenInclude(b => b.SeatReservations)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct) ?? throw new NotFoundException(nameof(PurchaseOrder), request.OrderId);
            if (order.Status != OrderStatus.Draft || order.ExpiresAt <= DateTimeOffset.UtcNow || order.ExpiresAt == null)
                throw new ConcurrencyException("Checkout không còn là Draft hợp lệ.");
            var holds = order.Bookings.SelectMany(b => b.SeatReservations).Where(r => r.Status == ReservationStatus.Pending).ToList();
            var passengers = request.Passengers ?? [];
            if (passengers.Count > 0 && (passengers.Count != holds.Count || passengers.Select(p => (p.BookingId, p.SeatId)).Distinct().Count() != holds.Count ||
                passengers.Any(p => !holds.Any(r => r.BookingId == p.BookingId && r.SeatId == p.SeatId))))
                throw new ConcurrencyException("Hành khách phải khớp đúng Booking và ghế đang giữ.");
            order.PurchaserName = request.CustomerName; order.PurchaserEmail = request.CustomerEmail; order.PurchaserPhone = request.CustomerPhone;
            foreach (var b in order.Bookings)
            {
                b.CustomerName = request.CustomerName; b.CustomerEmail = request.CustomerEmail; b.CustomerPhone = request.CustomerPhone;
                b.Status = BookingStatus.PendingPayment;
            }
            foreach (var r in holds)
            {
                var passenger = passengers.FirstOrDefault(p => p.BookingId == r.BookingId && p.SeatId == r.SeatId);
                r.PassengerName = passenger?.Name ?? request.CustomerName;
                r.PassengerEmail = passenger?.Email; r.PassengerPhone = passenger?.Phone;
            }
            order.ValidateForCheckout(); order.Status = OrderStatus.PendingPayment;
            await context.SaveChangesAsync(ct); return true;
        }, ct);
    }
}
