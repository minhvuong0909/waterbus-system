using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Fares;
using WaterbusSystem.Application.DTOs.Responses.Fares;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IFareAdminRepository
{
    Task<PaginatedList<FareRuleAdminResponse>> GetPageAsync(GetFareRulesRequest request, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<FareRuleAdminResponse> GetDetailAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>FareRule (có theo dõi) hoặc NotFound.</summary>
    Task<FareRule> GetTrackedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SeatClass?> FindSeatClassAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Bảng giá đang bật cùng TripType + SeatClass (để service xét giao khoảng hiệu lực).</summary>
    Task<List<FareRule>> ListActiveSiblingsAsync(string tripType, Guid seatClassId, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Đã có bảng giá (kể cả đã tắt/xoá mềm) cùng TripType + SeatClass + EffectiveFrom (unique index).</summary>
    Task<bool> ExistsWithSameStartAsync(string tripType, Guid seatClassId, DateTimeOffset effectiveFrom, Guid? excludeId, CancellationToken cancellationToken = default);

    void Add(FareRule fareRule);
}
