namespace WaterbusSystem.Application.DTOs.Requests.CheckIns;

public record CheckInEventInput(
    Guid TicketId,
    Guid TripStopCallId,
    string ClientEventId,
    DateTimeOffset OccurredAtDevice,
    string? Note);

/// <summary>
/// Đồng bộ theo lô các sự kiện check-in đã ghi offline (BR-OFF-01..05). Mỗi event được xử lý và LƯU
/// ĐỘC LẬP: 1 event lỗi không làm mất kết quả của các event khác đã đồng bộ thành công trong cùng lô.
/// Idempotent theo ClientEventId.
/// </summary>
public record SyncCheckInEventsRequest(IReadOnlyList<CheckInEventInput> Events);
