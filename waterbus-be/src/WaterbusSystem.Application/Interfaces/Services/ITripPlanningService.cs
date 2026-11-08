using WaterbusSystem.Application.DTOs.Requests.TripPlanning;
using WaterbusSystem.Application.DTOs.Responses.TripPlanning;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ITripPlanningService
{
    Task<GenerateTripsResponse> GenerateFromScheduleAsync(GenerateTripsRequest request, CancellationToken cancellationToken = default);
    Task<CreateAdHocTripResponse> CreateAdHocAsync(CreateAdHocTripRequest request, CancellationToken cancellationToken = default);
    Task<AssignTripBoatResponse> AssignBoatAsync(Guid tripId, AssignTripBoatRequest request, CancellationToken cancellationToken = default);
}
