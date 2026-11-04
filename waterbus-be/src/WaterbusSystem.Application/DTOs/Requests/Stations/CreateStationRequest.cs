namespace WaterbusSystem.Application.DTOs.Requests.Stations;

public record CreateStationRequest(
    string Code,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int OrderIndex);
