namespace WaterbusSystem.Application.DTOs.Requests.Incidents;

/// <summary>Bộ lọc và phân trang cho danh sách sự cố.</summary>
public record GetIncidentsRequest(
    string? Status = null,
    string? IncidentType = null,
    int PageNumber = 1,
    int PageSize = 20);
