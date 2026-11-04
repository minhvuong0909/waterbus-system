using WaterbusSystem.Application.DTOs.Responses.Fares;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IFareService
{
    /// <summary>Bảng giá công khai đang hiệu lực (chỉ đọc), sắp theo loại chuyến rồi giá tăng dần.</summary>
    Task<List<FareResponse>> GetActiveFaresAsync(CancellationToken cancellationToken = default);
}
