using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface ITripRepository
{
    /// <summary>
    /// Chuyến có trạng thái trong <paramref name="statuses"/>, khởi hành trước <paramref name="horizon"/>
    /// và chưa đến bến tại <paramref name="now"/>.
    /// </summary>
    Task<List<ActiveTripRef>> ListActiveInWindowAsync(
        IReadOnlyCollection<TripStatus> statuses, DateTimeOffset now, DateTimeOffset horizon, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyến mới nhất (theo giờ khởi hành) của tàu có trạng thái trong <paramref name="statuses"/>; null nếu không có.
    /// </summary>
    Task<Guid?> FindLatestTripIdByBoatAsync(
        Guid boatId, IReadOnlyCollection<TripStatus> statuses, CancellationToken cancellationToken = default);
}
