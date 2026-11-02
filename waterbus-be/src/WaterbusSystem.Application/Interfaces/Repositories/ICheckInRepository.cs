using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface ICheckInRepository
{
    // ---------------- Scanner devices / assignments ----------------

    /// <summary>Thiết bị quét (có theo dõi để sửa) hoặc ném NotFoundException.</summary>
    Task<ScannerDevice> GetRequiredDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Điểm dừng (có theo dõi để sửa) hoặc ném NotFoundException.</summary>
    Task<TripStopCall> GetRequiredStopCallAsync(Guid tripStopCallId, CancellationToken cancellationToken = default);

    /// <summary>BR-OFF-02: đã có Primary Offline Scanner khác hoạt động cùng khoảng thời gian cho điểm dừng này.</summary>
    Task<bool> HasOverlappingPrimaryAssignmentAsync(
        Guid tripStopCallId, DateTimeOffset activeFrom, DateTimeOffset requestedEnd, CancellationToken cancellationToken = default);

    void AddAssignment(ScannerAssignment assignment);

    /// <summary>Phân công (có theo dõi để sửa) hoặc ném NotFoundException.</summary>
    Task<ScannerAssignment> GetRequiredAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Staff có phân công còn hiệu lực tại <paramref name="now"/> cho điểm dừng này.</summary>
    Task<bool> HasActiveAssignmentAsync(Guid staffId, Guid tripStopCallId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<List<MyScannerAssignmentResponse>> ListMyAssignmentsAsync(
        Guid staffId, DateTimeOffset now, CancellationToken cancellationToken = default);

    // ---------------- Check-in events (idempotency) ----------------

    Task<CheckInEvent?> FindEventByClientIdAsync(string clientEventId, CancellationToken cancellationToken = default);

    /// <summary>Vé (chỉ đọc) kèm SeatReservation, dùng để trả lại kết quả cũ khi replay 1 ClientEventId đã xử lý.</summary>
    Task<Ticket?> GetTicketWithReservationAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>Vé (có theo dõi để sửa) kèm SeatReservation/Booking hoặc ném NotFoundException.</summary>
    Task<Ticket> GetRequiredTicketWithBookingAsync(Guid ticketId, CancellationToken cancellationToken = default);

    void AddCheckInEvent(CheckInEvent checkInEvent);

    // ---------------- Manifest ----------------

    /// <summary>Điểm dừng (chỉ đọc) kèm Route/Trip/Boat hoặc ném NotFoundException. Dùng cho manifest.</summary>
    Task<TripStopCall> GetStopCallForManifestAsync(Guid tripStopCallId, CancellationToken cancellationToken = default);

    /// <summary>Vé hợp lệ có bến lên đúng điểm dừng này (BR-BOARD-03), kèm SeatReservation/Booking.</summary>
    Task<List<Ticket>> ListTicketsForManifestAsync(Guid tripStopCallId, CancellationToken cancellationToken = default);

    // ---------------- NoShow finalization ----------------

    /// <summary>Id các điểm dừng đã đóng cửa sổ check-in trước <paramref name="deadline"/> (loại Trip Cancelled/Terminated).</summary>
    Task<List<Guid>> ListClosedStopCallIdsAsync(DateTimeOffset deadline, CancellationToken cancellationToken = default);

    /// <summary>Id các điểm dừng trong <paramref name="stopCallIds"/> còn CheckInEvent Pending/Conflict chưa đối soát.</summary>
    Task<List<Guid>> ListUnresolvedStopCallIdsAsync(IReadOnlyCollection<Guid> stopCallIds, CancellationToken cancellationToken = default);

    /// <summary>Vé Valid/NotCheckedIn (có theo dõi để sửa) có bến lên thuộc <paramref name="eligibleStopCallIds"/>.</summary>
    Task<List<Ticket>> ListNoShowCandidateTicketsAsync(IReadOnlyCollection<Guid> eligibleStopCallIds, CancellationToken cancellationToken = default);
}
