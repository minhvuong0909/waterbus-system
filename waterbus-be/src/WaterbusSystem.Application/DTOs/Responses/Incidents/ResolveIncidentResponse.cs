namespace WaterbusSystem.Application.DTOs.Responses.Incidents;

/// <summary>Kết quả đóng sự cố.</summary>
public record ResolveIncidentResponse(Guid IncidentId, string Status, DateTimeOffset ResolvedAt);
