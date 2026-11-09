using WaterbusSystem.Application.DTOs.Responses.SeatInventory;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IBoatSeatRepository
{
    /// <summary>Tàu (có theo dõi) hoặc ném NotFoundException. Service sửa UpdatedAt để tuần tự hoá thao tác trên cùng tàu.</summary>
    Task<Boat> GetBoatForUpdateAsync(Guid boatId, CancellationToken cancellationToken = default);

    /// <summary>Mọi Seat của tàu kể cả xoá mềm (có theo dõi) vì unique (BoatId, SeatCode) tính cả chúng.</summary>
    Task<List<Seat>> ListAllTrackedAsync(Guid boatId, CancellationToken cancellationToken = default);

    Task<List<BoatSeatResponse>> ListAsync(Guid boatId, CancellationToken cancellationToken = default);

    Task<List<SeatClass>> GetClassesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Ghế đang vướng đặt chỗ Pending/Confirmed trên chuyến chưa kết thúc (không đổi/ngừng được).</summary>
    Task<HashSet<Guid>> GetSeatIdsWithOpenCommitmentsAsync(IReadOnlyCollection<Guid> seatIds, CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<Seat> seats);
}
