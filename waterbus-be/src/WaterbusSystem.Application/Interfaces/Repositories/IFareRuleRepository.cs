using WaterbusSystem.Application.DTOs.Responses.Fares;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IFareRuleRepository
{
    /// <summary>Bảng giá đang hiệu lực tại <paramref name="now"/> (chưa xoá, đang active, hạng ghế còn hoạt động).</summary>
    Task<List<FareResponse>> ListActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
