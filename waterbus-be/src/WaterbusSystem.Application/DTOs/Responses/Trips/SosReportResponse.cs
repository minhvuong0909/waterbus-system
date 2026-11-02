namespace WaterbusSystem.Application.DTOs.Responses.Trips;

public record SosReportResponse(Guid IncidentId, DateTimeOffset ReportedAt);
