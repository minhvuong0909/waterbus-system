using WaterbusSystem.Application.DTOs.Responses.SeatInventory;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface ISeatClassRepository
{
    Task<List<SeatClassResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<SeatClassResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Hạng ghế (có theo dõi để sửa) hoặc ném NotFoundException.</summary>
    Task<SeatClass> GetRequiredByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Mã đã dùng (kể cả bản ghi xoá mềm, vì chỉ mục unique tính cả chúng).</summary>
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Có ghế của hạng này đang vướng đặt chỗ Pending/Confirmed trên chuyến chưa kết thúc.</summary>
    Task<bool> HasOpenCommitmentsAsync(Guid seatClassId, CancellationToken cancellationToken = default);

    void Add(SeatClass seatClass);
}
