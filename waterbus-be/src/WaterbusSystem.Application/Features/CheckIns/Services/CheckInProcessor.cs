using Microsoft.Extensions.Options;
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
    private readonly OfflineCheckInReconciliationOptions _reconciliation;

    public CheckInProcessor(ICheckInRepository repository, IOptions<OfflineCheckInReconciliationOptions>? reconciliation = null)
    {
        _repository = repository;
        _reconciliation = reconciliation?.Value ?? new OfflineCheckInReconciliationOptions();
    }

    public async Task<(CheckInOutcome Outcome, bool WasAlreadySynced)> ProcessAsync(
        Guid staffId,
        Guid ticketId,
        Guid tripStopCallId,
        string clientEventId,
        DateTimeOffset occurredAtDevice,
        string? note,
        DateTimeOffset now,
        bool isOfflineSync,
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

        // BR-OFF-04/05: occurredAtDevice do client tự khai báo, không được dùng làm mốc thời gian nghiệp vụ
        // một cách vô điều kiện (có thể bị lùi/giả). Quét trực tuyến (SubmitCheckInAsync) luôn dùng giờ server
        // (now) vì không có khoảng trễ cần đối soát. Quét offline (SyncCheckInEventsAsync) được phép dùng
        // occurredAtDevice LÀM MỐC ĐỐI SOÁT cửa sổ check-in và quyền tác nghiệp, nhưng chỉ khi occurredAtDevice
        // không ở tương lai so với now, VÀ không lùi quá MaxOfflineSkewHours so với now (cấu hình được, BR v2.0
        // không quy định số cụ thể - chỉ giới hạn phạm vi bất thường, không giải quyết triệt để việc giả mạo
        // đồng hồ thiết bị trong phạm vi ngưỡng). Vượt quá 1 trong 2 giới hạn -> báo lỗi riêng cho event này
        // (BR-OFF-03: không làm fail cả lô) thay vì âm thầm dùng now hoặc occurredAtDevice.
        var oldestTrustedDeviceTime = now.AddHours(-_reconciliation.MaxOfflineSkewHours);
        if (isOfflineSync && (occurredAtDevice > now || occurredAtDevice < oldestTrustedDeviceTime))
        {
            const string invalidOutcome = "InvalidDeviceTime";
            _repository.AddCheckInEvent(new CheckInEvent
            {
                TicketId = ticket.Id,
                StaffAccountId = staffId,
                TripStopCallId = tripStopCallId,
                ClientEventId = clientEventId,
                OccurredAtDevice = occurredAtDevice,
                ReceivedAtServer = now,
                Outcome = invalidOutcome,
                SyncStatus = "Synced",
                Notes = note
            });
            return (new CheckInOutcome(invalidOutcome, null, ticket.TicketCode, ticket.SeatReservation?.PassengerName), false);
        }

        // Mốc đối soát quyền tác nghiệp (BR-BOARD-03) và cửa sổ check-in (BR-BOARD-05): giờ server khi trực
        // tuyến, giờ quét thật trên máy khi offline - ScannerAssignment.ActiveFrom/ActiveUntil đã là khung thời
        // gian scanner được phân công tác nghiệp, dùng lại đúng khung đó để chặn occurredAtDevice vô lý, không
        // cần phát sinh ngưỡng nghiệp vụ mới.
        var effectiveAt = isOfflineSync ? occurredAtDevice : now;

        var hasAssignment = await _repository.HasActiveAssignmentAsync(staffId, tripStopCallId, effectiveAt, cancellationToken);

        var outcome = DetermineOutcome(hasAssignment, tripStopCallId, booking, stopCall, effectiveAt, ticket);

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
        DateTimeOffset effectiveAt, Ticket ticket)
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
            effectiveAt < stopCall.CheckInOpenAt || effectiveAt > stopCall.CheckInCloseAt)
        {
            // BR-BOARD-05: cửa sổ check-in theo đúng bến lên, không phải một mốc chung toàn Trip.
            return "OutOfWindow";
        }

        if (ticket.Status != TicketStatus.Valid)
        {
            return "TicketInvalid";
        }

        if (ticket.BoardingStatus == BoardingStatus.NoShow)
        {
            // BR-OFF-04: "không xem kết quả offline là bằng chứng tuyệt đối trước khi backend reconciliation,
            // đặc biệt khi xử lý NoShow". RunNoShowFinalizationAsync đã kết luận NoShow (ghi NoShowFinalizedAt) -
            // 1 lần quét đến muộn (online hay offline) KHÔNG được tự động đảo ngược lại CheckedIn mà không qua
            // đối soát rõ ràng; vẫn ghi CheckInEvent để có vết cho Admin xem xét thủ công nếu cần phục hồi vé.
            return "NoShowFinalized";
        }

        if (ticket.BoardingStatus == BoardingStatus.CheckedIn)
        {
            // BR-QR-03: cho phép quét lại; báo đã check-in trước đó, không coi là lỗi.
            return "AlreadyCheckedIn";
        }

        return "Success";
    }
}
