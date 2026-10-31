namespace WaterbusSystem.Application.DTOs.Responses.Incidents;

// Một dòng trong danh sách sự cố của dashboard điều phối
public record IncidentSummaryResponse(
    Guid Id,
    string ScopeType,
    Guid? RouteId,
    string? RouteCode,
    Guid? BoatId,
    string Source,
    string IncidentType,
    string? Severity,
    string Status,
    string? Description,
    DateTimeOffset? OccurredAt,
    DateTimeOffset? ResolvedAt,
    int AffectedTripCount,
    int PendingDecisionCount);