namespace WaterbusSystem.Application.DTOs.Responses.Fares;

/// <summary><paramref name="Status"/>: Inactive, Upcoming, Active hoặc Expired (tính theo giờ hiện tại).</summary>
public record FareRuleAdminResponse(
    Guid Id, string TripType, Guid SeatClassId, string SeatClassCode, string SeatClassName, decimal Price, string Currency,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, bool IsActive, string Status);
