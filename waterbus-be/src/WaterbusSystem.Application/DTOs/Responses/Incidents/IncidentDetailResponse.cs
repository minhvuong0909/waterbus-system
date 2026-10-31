using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Responses.Incidents;

// Một chuyến bị sự cố ảnh hưởng, kèm quyết định của Admin (nếu đã có).
public record AffectedTripResponse(
    Guid IncidentTripId,
    Guid TripId,
    DateTimeOffset DepartureTime,
    TripStatus TripStatus,
    Guid? BoatId,
    string? OperationDecision,
    Guid? DecisionByAdminId,
    DateTimeOffset? DecisionAt
);

// Chi tiết sự cố kèm các chuyến bị ảnh hưởng
public record IncidentDetailResponse(
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
    List<AffectedTripResponse> AffectedTrips
);
