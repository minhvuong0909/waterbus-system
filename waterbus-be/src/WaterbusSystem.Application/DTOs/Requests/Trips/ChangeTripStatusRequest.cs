using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Trips;

/// <summary>Thân yêu cầu đổi trạng thái chuyến; <c>operation</c> là tên thao tác (vd "Suspend") hoặc số.</summary>
public record ChangeTripStatusRequest(TripOperation Operation, string? Reason, Guid? IncidentId);
