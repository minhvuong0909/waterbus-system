namespace WaterbusSystem.Application.DTOs.Responses.Stations;

/// <summary>
/// DTO chứa thông tin bến tàu trả về cho Web/Mobile
/// </summary>
public record StationResponse(
    Guid Id,
    string Code,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int OrderIndex,
    bool IsActive);
