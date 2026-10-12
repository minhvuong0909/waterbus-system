using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Sự kiện check-in của 1 Ticket tại 1 bến dừng.
/// ClientEventId là deduplication key cho offline sync — UNIQUE.
/// Outcome: "Success", "AlreadyCheckedIn", "InvalidStop", "TicketInvalid", "Conflict"
/// </summary>
public class CheckInEvent : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public Guid StaffAccountId { get; set; }
    public Guid? ScannerDeviceId { get; set; }
    public ScannerDevice? ScannerDevice { get; set; }
    public Guid TripStopCallId { get; set; }
    public TripStopCall? TripStopCall { get; set; }
    /// <summary>Deduplication key sinh từ thiết bị offline — UNIQUE</summary>
    public string ClientEventId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtDevice { get; set; }
    public DateTimeOffset? ReceivedAtServer { get; set; }
    public string Outcome { get; set; } = string.Empty;
    /// <summary>"Synced", "Pending", "Conflict"</summary>
    public string SyncStatus { get; set; } = "Synced";
    public string? Notes { get; set; }
}
