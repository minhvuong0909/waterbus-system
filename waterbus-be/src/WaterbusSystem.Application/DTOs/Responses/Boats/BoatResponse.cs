namespace WaterbusSystem.Application.DTOs.Responses.Boats;

public record BoatResponse(
    Guid Id,
    string Code,
    string Name,
    string RegistrationNumber,
    int TotalSeats,
    string Manufacturer,
    int YearBuilt,
    bool IsActive,
    Guid? CaptainUserId);
