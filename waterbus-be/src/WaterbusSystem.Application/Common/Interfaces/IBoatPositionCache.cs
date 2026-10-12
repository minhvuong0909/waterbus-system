namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Lưu vị trí MỚI NHẤT của mỗi tàu (Redis) để client đồng bộ lại sau khi reconnect
/// mà không phải đọc lịch sử trong SQL. Không phải nguồn sự thật lâu dài.
/// </summary>
public interface IBoatPositionCache
{
    Task<BoatPositionMessage?> GetAsync(Guid boatId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BoatPositionMessage>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SetAsync(BoatPositionMessage message, CancellationToken cancellationToken = default);
}
