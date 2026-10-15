using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Payments.Commands.CreateVnPayUrl;

public record CreateVnPayUrlResponseDto(string PaymentUrl, string OrderId, string MerchantReference, decimal Amount, DateTimeOffset ExpiresAt);
public record CreateVnPayUrlCommand(Guid OrderId, string IpAddress, string? AccessToken = null) : IRequest<CreateVnPayUrlResponseDto>;

public sealed class CreateVnPayUrlCommandHandler(IApplicationDbContext context, IVnPayService vnPay,
    ICommercialTransactionRunner transactions, CheckoutOptions options, CheckoutAccessService access,
    ICheckoutSessionService? sessions = null) : IRequestHandler<CreateVnPayUrlCommand, CreateVnPayUrlResponseDto>
{
    public async Task<CreateVnPayUrlResponseDto> Handle(CreateVnPayUrlCommand request, CancellationToken ct)
    {
        await access.RequireAsync(request.OrderId, request.AccessToken, ct);
        var result = await transactions.RunAsync(async () =>
        {
            var order = await context.PurchaseOrders.Include(o => o.Bookings).ThenInclude(b => b.SeatReservations)
                .Include(o => o.Bookings).ThenInclude(b => b.Trip)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct) ?? throw new NotFoundException(nameof(PurchaseOrder), request.OrderId);
            var now = DateTimeOffset.UtcNow;
            if (order.Status != OrderStatus.PendingPayment || order.ExpiresAt <= now || order.ExpiresAt == null ||
                order.Bookings.Any(b => b.Status != BookingStatus.PendingPayment ||
                    b.Trip == null || b.Trip.Status is not (TripStatus.Scheduled or TripStatus.Boarding) || b.Trip.ActualDepartureTime != null))
                throw new ConcurrencyException("Đơn hàng đã hết hạn hoặc không còn chờ thanh toán.");
            var attempt = await context.FinancialTransactions.FirstOrDefaultAsync(p => p.OrderId == order.Id &&
                p.TransactionType == FinancialTransactionType.Payment && p.Status == FinancialTransactionStatus.Requested, ct);
            if (attempt is null)
            {
                if (!await context.FinancialTransactions.AnyAsync(p => p.OrderId == order.Id && p.TransactionType == FinancialTransactionType.Payment, ct))
                    order.ExpiresAt = now.Add(options.PaymentDuration);
                attempt = new FinancialTransaction { Order = order, OrderId = order.Id, Amount = order.QuotedTotal,
                    Currency = order.Currency, GatewayName = "VNPAY", TransactionType = FinancialTransactionType.Payment,
                    GatewayRequestCreatedAt = now, Status = FinancialTransactionStatus.Requested };
                attempt.MerchantReference = attempt.Id.ToString("N");
                attempt.IdempotencyKey = "PAY_" + attempt.Id.ToString("N");
                context.FinancialTransactions.Add(attempt);
            }
            var url = vnPay.CreatePaymentUrl(attempt.MerchantReference!, attempt.Amount,
                "Thanh toan waterbus", request.IpAddress, attempt.GatewayRequestCreatedAt, order.ExpiresAt);
            await context.SaveChangesAsync(ct);
            return new CreateVnPayUrlResponseDto(url, order.Id.ToString(), attempt.MerchantReference!, attempt.Amount, order.ExpiresAt!.Value);
        }, ct);
        if (sessions is not null)
            try
            {
                var sessionId = "order:" + request.OrderId;
                if (await sessions.GetAsync(sessionId, ct) is null)
                {
                    var holds = await context.SeatReservations.AsNoTracking().Where(r => r.Booking!.OrderId == request.OrderId && r.Status == ReservationStatus.Pending)
                        .Select(r => new SeatHold(r.TripId, r.SeatId, r.BoardingStopOrder, r.DisembarkingStopOrder)).ToListAsync(ct);
                    await sessions.HoldUntilAsync(sessionId, request.OrderId, holds, result.ExpiresAt, ct);
                }
                await sessions.MarkPaymentPendingUntilAsync(sessionId, request.OrderId, result.ExpiresAt, ct);
            }
            catch (Exception) { /* DB là nguồn sự thật của cửa sổ chờ thanh toán. */ }
        return result;
    }
}
