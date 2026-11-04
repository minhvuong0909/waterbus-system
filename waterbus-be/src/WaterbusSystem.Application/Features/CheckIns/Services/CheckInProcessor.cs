using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.CheckIns.Services;

public record CheckInOutcome(string Outcome, DateTimeOffset? CheckedInAt, string? TicketCode, string? PassengerName);

/// <summary>
/// Logic xử lý 1 lần quét check-in, dùng chung cho luồng trực tuyến (CheckInService.SubmitCheckInAsync)
/// và đồng bộ theo lô từ offline scanner (CheckInService.SyncCheckInEventsAsync) - BR-QR-03, BR-BOARD-03/05, BR-OFF-03/05.
/// KHÔNG tự lưu: caller quyết định lưu từng lần (để 1 event lỗi trong batch không
/// làm rollback các event khác đã xử lý thành công - BR-OFF-03).
/// </summary>
public class CheckInProcessor
{
    private readonly ICheckInRepository _repository;

    public CheckInProcessor(ICheckInRepository repository)
    {
        _repository = repository;
    }

    public async Task<(CheckInOutcome Outcome, bool WasAlreadySynced)> ProcessAsync(
        Guid staffId,
        Guid ticketId,
        Guid tripStopCallId,
        string clientEventId,
        DateTimeOffset occurredAtDevice,
        string? note,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Khử trùng trước mọi việc khác (BR-OFF-03): quét/gửi lại cùng ClientEventId -> trả đúng kết quả cũ.
        var existingEvent = await _repository.FindEventByClientIdAsync(clientEventId, cancellationToken);
        if (existingEvent is not null)
        {
            var existingTicket = await _repository.GetTicketWithReservationAsync(existingEvent.TicketId, cancellationToken);

            return (new CheckInOutcome(
                existingEvent.Outcome,
                existingTicket?.CheckedInAt,
                existingTicket?.TicketCode,
                existingTicket?.SeatReservation?.PassengerName), true);
        }

        var stopCall = await _repository.GetRequiredStopCallAsync(tripStopCallId, cancellationToken);
        var ticket = await _repository.GetRequiredTicketWithBookingAsync(ticketId, cancellationToken);

        var booking = ticket.SeatReservation?.Booking
            ?? throw new NotFoundException(nameof(Booking), ticketId);

        // BR-BOARD-03: quyền tác nghiệp Staff cho đúng điểm dừng (có 1 ScannerAssignment hợp lệ là đủ -
        // không cần là Primary Offline, ràng buộc đó chỉ áp dụng khi gán scanner - BR-OFF-02).
        var hasAssignment = await _repository.HasActiveAssignmentAsync(staffId, tripStopCallId, now, cancellationToken);

        var outcome = DetermineOutcome(hasAssignment, tripStopCallId, booking, stopCall, now, ticket);

        _repository.AddCheckInEvent(new CheckInEvent
        {
            TicketId = ticket.Id,
            StaffAccountId = staffId,
            TripStopCallId = tripStopCallId,
            ClientEventId = clientEventId,
            OccurredAtDevice = occurredAtDevice,
            ReceivedAtServer = now,
            Outcome = outcome,
            SyncStatus = "Synced",
            Notes = note
        });

        if (outcome == "Success")
        {
            ticket.BoardingStatus = BoardingStatus.CheckedIn;
            ticket.CheckedInAt = now;
            ticket.CheckedInByStaffId = staffId;
        }

        return (new CheckInOutcome(
            outcome,
            outcome == "Success" ? now : null,
            ticket.TicketCode,
            ticket.SeatReservation?.PassengerName), false);
    }

    private static string DetermineOutcome(bool hasAssignment, Guid tripStopCallId, Booking booking, TripStopCall stopCall,
        DateTimeOffset now, Ticket ticket)
    {
        if (!hasAssignment)
        {
            return "NotAssigned";
        }

        if (tripStopCallId != booking.BoardingCallId)
        {
            // BR-BOARD-03: không được check-in Ticket tại bến khác bến lên của chính Booking đó.
            return "InvalidStop";
        }

        if (stopCall.CheckInOpenAt is null || stopCall.CheckInCloseAt is null ||
            now < stopCall.CheckInOpenAt || now > stopCall.CheckInCloseAt)
        {
            // BR-BOARD-05: cửa sổ check-in theo đúng bến lên, không phải một mốc chung toàn Trip.
            return "OutOfWindow";
        }

        if (ticket.Status != TicketStatus.Valid)
        {
            return "TicketInvalid";
        }

        if (ticket.BoardingStatus == BoardingStatus.CheckedIn)
        {
            // BR-QR-03: cho phép quét lại; báo đã check-in trước đó, không coi là lỗi.
            return "AlreadyCheckedIn";
        }

        return "Success";
    }
}
