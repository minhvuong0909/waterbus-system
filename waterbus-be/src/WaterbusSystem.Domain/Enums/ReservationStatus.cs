namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Trạng thái giữ chỗ phân đoạn (Segment Reservation)
/// </summary>
public enum ReservationStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3,
    Completed = 4
}
