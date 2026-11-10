using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Truy vấn chỉ đọc để tìm các Booking của một chuyến cần hoàn tiền (BR-RF-01 → BR-RF-09).</summary>
public interface IRefundCandidateRepository
{
    /// <summary>Chuyến (kèm tuyến), ném NotFoundException nếu không tồn tại.</summary>
    Task<Trip> GetTripAsync(Guid tripId, CancellationToken ct);

    /// <summary>Booking của chuyến có ít nhất một vé, kèm Order, Tickets và điểm xuống (để đánh giá điều kiện hoàn).</summary>
    Task<IReadOnlyList<Booking>> ListBookingsWithTicketsAsync(Guid tripId, CancellationToken ct);

    /// <summary>Vé của chuyến đã nằm trong refund Requested/Processing/Succeeded (không được hoàn lần hai).</summary>
    Task<IReadOnlySet<Guid>> GetAllocatedTicketIdsAsync(Guid tripId, CancellationToken ct);
}
