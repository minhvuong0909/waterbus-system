namespace WaterbusSystem.Application.DTOs.Requests.Tracking;

/// <summary>Lịch sử vị trí đã lưu của một tàu trong khoảng thời gian, có phân trang.</summary>
public record GetBoatPositionHistoryRequest(
    Guid BoatId,
    DateTimeOffset From,
    DateTimeOffset To,
    int PageNumber = 1,
    int PageSize = 100);
