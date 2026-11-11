namespace WaterbusSystem.Application.DTOs.Responses.Incidents;

/// <summary>Kết quả một lượt quét thời tiết của Weather Guard.</summary>
public record WeatherGuardResponse(
    int RoutesChecked,
    int IncidentsCreated,
    int IncidentsUpdated,
    int ObservationFailures,
    int IncidentsResolved = 0);
