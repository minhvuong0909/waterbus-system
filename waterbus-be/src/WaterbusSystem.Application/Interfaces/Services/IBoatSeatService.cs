using WaterbusSystem.Application.DTOs.Requests.SeatInventory;
using WaterbusSystem.Application.DTOs.Responses.SeatInventory;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IBoatSeatService
{
    Task<BoatSeatInventoryResponse> GetInventoryAsync(Guid boatId, CancellationToken cancellationToken = default);
    Task<BoatSeatInventoryResponse> AddSeatsAsync(Guid boatId, AddBoatSeatsRequest request, CancellationToken cancellationToken = default);
    Task<BoatSeatInventoryResponse> UpdateSeatAsync(Guid boatId, Guid seatId, UpdateBoatSeatRequest request, CancellationToken cancellationToken = default);
    Task<BoatSeatInventoryResponse> DeactivateSeatAsync(Guid boatId, Guid seatId, CancellationToken cancellationToken = default);
    Task<BoatSeatInventoryResponse> ActivateSeatAsync(Guid boatId, Guid seatId, CancellationToken cancellationToken = default);
}
