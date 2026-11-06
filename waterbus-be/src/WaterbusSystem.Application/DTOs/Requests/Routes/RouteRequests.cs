namespace WaterbusSystem.Application.DTOs.Requests.Routes;

/// <summary>Một điểm dừng theo thứ tự trong Route (SequenceNo = vị trí trong danh sách, bắt đầu từ 1).</summary>
public record RouteStopInput(Guid StationId, bool CanBoard = true, bool CanAlight = true);

public record CreateRouteRequest(
    string Code, string Name, string ServiceType, int EstimatedDurationMinutes, decimal DistanceKm, List<RouteStopInput> Stops);

public record UpdateRouteRequest(string Name, string ServiceType, int EstimatedDurationMinutes, decimal DistanceKm);

public record ReplaceRouteStopsRequest(List<RouteStopInput> Stops);

public record GetRoutesRequest(int PageNumber = 1, int PageSize = 10, bool? IsActive = null, string? ServiceType = null);
