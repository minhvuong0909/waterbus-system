using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Features.Bookings.Services;

public sealed class CheckoutCommitEffects(
    IOrderRepository orders,
    IBookingRepository bookings,
    ICheckoutSessionService sessions,
    ILogger<CheckoutCommitEffects> logger,
    ISeatEventPublisher? events = null)
{
    public async Task HeldAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetRequiredByIdAsync(
            orderId, cancellationToken);

        if (order.Status is not (
                OrderStatus.Draft or OrderStatus.PendingPayment) ||
            order.ExpiresAt is null ||
            order.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        var reservations =
            await bookings.GetPendingReservationsByOrderAsync(
                orderId, cancellationToken);

        try
        {
            await sessions.HoldUntilAsync(
                $"order:{orderId}",
                orderId,
                reservations.Select(r => new SeatHold(
                    r.TripId,
                    r.SeatId,
                    r.BoardingStopOrder,
                    r.DisembarkingStopOrder)).ToList(),
                order.ExpiresAt.Value,
                cancellationToken);
        }
        catch (Exception)
        {
            logger.LogWarning(
                "Không đồng bộ được cache checkout; " +
                "DB vẫn giữ reservation và deadline.");
        }

        await CheckoutSeatEvents.PublishAsync(
            events,
            reservations,
            "Held",
            order.ExpiresAt,
            cancellationToken);
    }


    public async Task ReleasedAsync(
        Guid orderId,
        IReadOnlyCollection<SeatReservation> released,
        CancellationToken cancellationToken = default)
    {
        await CheckoutSeatEvents.PublishAsync(
            events,
            released,
            "Released",
            null,
            cancellationToken);

        try
        {
            await sessions.ReleaseAsync(
                $"order:{orderId}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Không thể xóa Redis checkout session cho Order {OrderId}",
                orderId);
        }
    }


    public async Task SeatsUpdatedAsync(
        Guid orderId,
        IReadOnlyCollection<SeatReservation> released,
        IReadOnlyCollection<SeatReservation> added,
        CancellationToken cancellationToken = default)
    {
        var order = await orders.GetRequiredByIdAsync(
            orderId, cancellationToken);

        var current = await bookings
            .GetPendingReservationsByOrderAsync(
                orderId, cancellationToken);

        try
        {
            if (order.ExpiresAt is { } expiresAt &&
                expiresAt > DateTimeOffset.UtcNow)
            {
                await sessions.HoldUntilAsync(
                    $"order:{orderId}",
                    orderId,
                    current.Select(r => new SeatHold(
                        r.TripId,
                        r.SeatId,
                        r.BoardingStopOrder,
                        r.DisembarkingStopOrder)).ToList(),
                    expiresAt,
                    cancellationToken);
            }
            else
            {
                await sessions.ReleaseAsync(
                    $"order:{orderId}",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Không đồng bộ được Redis sau đổi ghế Order {OrderId}",
                orderId);
        }

        await CheckoutSeatEvents.PublishAsync(
            events,
            released,
            "Released",
            null,
            cancellationToken);

        await CheckoutSeatEvents.PublishAsync(
            events,
            added,
            "Held",
            order.ExpiresAt,
            cancellationToken);
    }


}
