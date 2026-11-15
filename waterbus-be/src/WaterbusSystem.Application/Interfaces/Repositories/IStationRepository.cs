using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.DTOs.Responses.Stations;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IStationRepository
{
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đã có bến (chưa xóa) khác nằm đúng tọa độ này, sai khác dưới nửa đơn vị ở chữ số thập phân thứ 7 (độ chính xác lưu trữ).
    /// <paramref name="excludeId"/> loại chính bến đang sửa.
    /// </summary>
    Task<bool> ExistsAtCoordinatesAsync(double latitude, double longitude, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Bến (có theo dõi để sửa) hoặc ném NotFoundException.</summary>
    Task<Station> GetRequiredByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết bến (chỉ đọc) hoặc ném NotFoundException.</summary>
    Task<StationResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Mọi bến đang hoạt động, sắp theo OrderIndex (luồng sông).</summary>
    Task<List<StationResponse>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<PaginatedList<StationResponse>> GetPageAsync(GetStationsPagedRequest request, CancellationToken cancellationToken = default);

    void Add(Station station);
}
