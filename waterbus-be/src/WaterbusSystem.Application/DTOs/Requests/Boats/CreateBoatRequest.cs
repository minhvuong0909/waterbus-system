namespace WaterbusSystem.Application.DTOs.Requests.Boats;

public record CreateBoatRequest(
    string Code,
    string Name,
    string RegistrationNumber,
    int TotalSeats,
    string Manufacturer,
    int YearBuilt,
    Guid? CaptainUserId);
