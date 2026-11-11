namespace WaterbusSystem.Application.DTOs.Requests.Bookings;

public sealed record UpdateCheckoutSeatsRequest(
    List<Guid> SeatIds,
    IReadOnlyList<UpdateCheckoutPassengerRequest> Passengers);

public sealed record UpdateCheckoutPassengerRequest(
    string Name,
    string? Email,
    string? Phone);
