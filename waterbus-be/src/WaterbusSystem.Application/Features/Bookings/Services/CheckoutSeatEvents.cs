using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Features.Bookings.Services;

/// <summary>Notification chỉ sau commit; lỗi transport không rollback giao dịch đã lưu.</summary>
public static class CheckoutSeatEvents
{
    public static async Task PublishAsync(ISeatEventPublisher? publisher, IEnumerable<SeatReservation> reservations,
        string status, DateTimeOffset? expiresAt, CancellationToken ct)
    {
        if (publisher is null) return;
        foreach (var r in reservations)
        {
            var message = new SeatEventMessage(r.TripId, r.SeatId, r.BoardingStopOrder,
                r.DisembarkingStopOrder, status, expiresAt);
            try
            {
                await (status switch
                {
                    "Held" => publisher.SeatHeldAsync(message, ct),
                    "Sold" => publisher.SeatSoldAsync(message, ct),
                    _ => publisher.SeatReleasedAsync(message, ct)
                });
            }
            catch (Exception) { /* REST là nguồn sự thật sau reconnect. */ }
        }
    }
}
