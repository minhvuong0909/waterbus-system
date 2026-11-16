namespace WaterbusSystem.Application.DTOs.Responses.Incidents;

// Một dòng trong danh sách sự cố của dashboard điều phối
public record IncidentSummaryResponse(
    Guid Id,
    string ScopeType,
    Guid? RouteId,
    string? RouteCode,
    Guid? BoatId,
    /// <summary>Mã tàu của sự cố phạm vi Boat/Trip có tàu (BR-INC-02); null nếu sự cố không gắn tàu.</summary>
    string? BoatCode,
    string Source,
    string IncidentType,
    string? Severity,
    string Status,
    string? Description,
    DateTimeOffset? OccurredAt,
    DateTimeOffset? ResolvedAt,
    int AffectedTripCount,
    int PendingDecisionCount);