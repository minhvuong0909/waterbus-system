using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.CheckIns.Services;

/// <summary>
/// Check-in vé tại bến (Staff Scanner) và quản trị phân công scanner (BR-QR-03, BR-BOARD-01..05,
/// BR-OFF-01..05, BR-CFG-01).
/// </summary>
public sealed class CheckInService(
    ICheckInRepository checkIns,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeService dateTime,
    CheckInProcessor processor,
    IScannerAssignmentLock assignmentLock,
    IOptions<NoShowFinalizationOptions> noShowOptions,
    ILogger<CheckInService> logger) : ICheckInService
{
    /// <summary>Thời gian chờ 1 request khác đang giữ khoá cùng TripStopCallId xong.</summary>
    private static readonly TimeSpan AssignmentLockWait = TimeSpan.FromSeconds(10);

    public Task<PaginatedList<ScannerStaffResponse>> ListScannerStaffAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
        checkIns.ListScannerStaffAsync(pageNumber, pageSize, cancellationToken);

    public async Task<PaginatedList<ScannerAssignmentAdminResponse>> ListScannerAssignmentsAsync(
        Guid tripStopCallId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        await checkIns.GetRequiredStopCallAsync(tripStopCallId, cancellationToken);
        return await checkIns.ListScannerAssignmentsAsync(tripStopCallId, dateTime.Now, pageNumber, pageSize, cancellationToken);
    }

    // ---------------- AssignScanner ----------------

    public async Task<AssignScannerResponse> AssignScannerAsync(AssignScannerRequest request, CancellationToken cancellationToken = default)
    {
        if (!await checkIns.IsEligibleScannerStaffAsync(request.StaffAccountId, cancellationToken))
            throw new ValidationException([new("staffAccountId", "Chọn tài khoản Staff đang hoạt động.")]);

        // Review PR #63: khoá DeviceId (mọi loại phân công, kể cả Secondary) TRƯỚC khi đọc/kiểm tra thiết bị, giữ tới khi
        // SaveChanges xong - loại trừ với ScannerDeviceAdminService.DeactivateAsync. Thứ tự: DeviceId -> TripStopCallId.
        await using var deviceGate = await assignmentLock.TryAcquireDeviceAsync(request.DeviceId, AssignmentLockWait, cancellationToken)
            ?? throw new ConflictException("Thiết bị đang được xử lý bởi yêu cầu khác. Vui lòng thử lại.");

        // Đọc lại SAU khi có khoá (không dựa vào kiểm tra trước khoá).
        var device = await checkIns.GetRequiredDeviceAsync(request.DeviceId, cancellationToken);

        if (!device.IsActive)
        {
            throw new ConflictException("Thiết bị đã bị ngưng hoạt động, không thể phân công.");
        }

        var stopCall = await checkIns.GetRequiredStopCallAsync(request.TripStopCallId, cancellationToken);

        // Review PR #58 (High - "Enforce the complete Primary Offline Scanner overlap invariant"):
        // check-then-insert (HasOverlappingPrimaryAssignmentAsync rồi mới Add+SaveChanges) không tự
        // loại trừ lẫn nhau giữa 2 request đồng thời - cả 2 có thể cùng thấy "chưa chồng lấn" trước
        // khi bên nào insert xong. Unique filtered index ở DB (CHK-03) chỉ phủ đúng case Primary vô
        // thời hạn (ActiveUntil IS NULL), KHÔNG phủ 2 khoảng hữu hạn chồng lấn giờ - đây chính là
        // khoảng hở BR-OFF-02 còn lại. Giữ khoá (sp_getapplock theo TripStopCallId) suốt từ lúc
        // kiểm tra overlap tới khi SaveChangesAsync commit/rollback, chỉ release ở finally - không
        // acquire khi là Secondary assignment (không tham gia bất biến "chỉ 1 Primary" nên không
        // cần serialize).
        IAsyncDisposable? gate = null;
        try
        {
            if (request.IsPrimaryOffline)
            {
                gate = await assignmentLock.TryAcquireAsync(request.TripStopCallId, AssignmentLockWait, cancellationToken)
                    ?? throw new ConflictException("Đang có yêu cầu phân công khác xử lý cho điểm dừng này. Vui lòng thử lại.");

                var requestedEnd = request.ActiveUntil ?? DateTimeOffset.MaxValue;

                // BR-OFF-02: chỉ 1 Primary Offline Scanner hoạt động cùng lúc cho cùng điểm dừng.
                var hasOverlappingPrimary = await checkIns.HasOverlappingPrimaryAssignmentAsync(
                    request.TripStopCallId, request.ActiveFrom, requestedEnd, cancellationToken);

                if (hasOverlappingPrimary)
                {
                    throw new ConflictException(
                        "Điểm dừng này đã có Primary Offline Scanner khác đang hoạt động trong khoảng thời gian này. " +
                        "Vui lòng thu hồi phân công cũ trước.");
                }
            }

            var assignment = new ScannerAssignment
            {
                DeviceId = device.Id,
                Device = device,
                StaffAccountId = request.StaffAccountId,
                TripStopCallId = stopCall.Id,
                TripStopCall = stopCall,
                IsPrimaryOffline = request.IsPrimaryOffline,
                ActiveFrom = request.ActiveFrom,
                ActiveUntil = request.ActiveUntil
            };
            checkIns.AddAssignment(assignment);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new AssignScannerResponse(
                assignment.Id, assignment.DeviceId, assignment.StaffAccountId, assignment.TripStopCallId,
                assignment.IsPrimaryOffline, assignment.ActiveFrom, assignment.ActiveUntil);
        }
        finally
        {
            if (gate is not null) await gate.DisposeAsync();
        }
    }

    // ---------------- RevokeScannerAssignment ----------------

    public async Task<RevokeScannerAssignmentResponse> RevokeScannerAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await checkIns.GetRequiredAssignmentAsync(assignmentId, cancellationToken);

        var now = dateTime.Now;

        if (assignment.ActiveFrom >= now && (assignment.ActiveUntil is null || assignment.ActiveUntil > assignment.ActiveFrom))
        {
            // Chưa từng có hiệu lực (phân công cho tương lai) - thu hồi hẳn, không để lại cửa sổ ảo.
            assignment.ActiveUntil = assignment.ActiveFrom;
        }
        else if (assignment.ActiveUntil is null || (assignment.ActiveFrom < now && assignment.ActiveUntil > now))
        {
            assignment.ActiveUntil = now;
        }
        // Đã kết thúc từ trước thì giữ nguyên (thao tác idempotent).

        assignment.UpdatedAt = now;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RevokeScannerAssignmentResponse(assignment.Id, assignment.ActiveUntil!.Value);
    }

    // ---------------- SetCheckInWindow ----------------

    public async Task<SetCheckInWindowResponse> SetCheckInWindowAsync(Guid tripStopCallId, SetCheckInWindowRequest request, CancellationToken cancellationToken = default)
    {
        var stopCall = await checkIns.GetRequiredStopCallAsync(tripStopCallId, cancellationToken);

        // Điểm dừng đã khởi hành/bị bỏ qua thì không còn ý nghĩa để cấu hình cửa sổ check-in.
        if (stopCall.Status is "Departed" or "Skipped")
        {
            throw new ConflictException("Không thể đặt cửa sổ check-in cho điểm dừng đã khởi hành hoặc bị bỏ qua.");
        }

        stopCall.CheckInOpenAt = request.CheckInOpenAt;
        stopCall.CheckInCloseAt = request.CheckInCloseAt;
        stopCall.UpdatedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetCheckInWindowResponse(stopCall.Id, request.CheckInOpenAt, request.CheckInCloseAt);
    }

    // ---------------- SubmitCheckIn ----------------

    public async Task<CheckInResultResponse> SubmitCheckInAsync(SubmitCheckInRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(currentUser.UserId, out var staffId) ||
            !string.Equals(currentUser.UserRole, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            // BR-ROLE-05: check-in là nghiệp vụ của Staff; không mở rộng cho Admin ở đây.
            throw new ForbiddenAccessException("Chỉ nhân viên soát vé mới được check-in.");
        }

        var now = dateTime.Now;

        var (outcome, _) = await processor.ProcessAsync(
            staffId, request.TicketId, request.TripStopCallId, request.ClientEventId,
            request.OccurredAtDevice, request.Note, now, isOfflineSync: false, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            // CHK-02: ClientEventId trùng được gửi gần như đồng thời (double-tap/retry mạng do
            // timeout) - request thua cuộc đọc lại kết quả request thắng đã commit, trả đúng
            // idempotent thay vì để lỗi hạ tầng rơi ra 500. Dùng lại ĐÚNG quy tắc kiểm tra identity
            // của CHK-06 (ResolveExistingEventOutcomeAsync) - nếu chỉ đọc thẳng existing.Outcome như
            // trước, request thua của 1 race với ticket/staff KHÁC bản ghi đã thắng sẽ bị âm thầm
            // trả kết quả của request kia (lộ ticketCode/passengerName cho Staff không liên quan).
            unitOfWork.DiscardTrackedChanges();
            var existing = await checkIns.FindEventByClientIdAsync(request.ClientEventId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            var (recoveredOutcome, _) = await processor.ResolveExistingEventOutcomeAsync(
                existing, staffId, request.TicketId, request.TripStopCallId, cancellationToken);
            return new CheckInResultResponse(
                recoveredOutcome.Outcome, recoveredOutcome.CheckedInAt, recoveredOutcome.TicketCode, recoveredOutcome.PassengerName);
        }

        return new CheckInResultResponse(outcome.Outcome, outcome.CheckedInAt, outcome.TicketCode, outcome.PassengerName);
    }

    // ---------------- SyncCheckInEvents ----------------

    public async Task<SyncCheckInEventsResponse> SyncCheckInEventsAsync(SyncCheckInEventsRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(currentUser.UserId, out var staffId) ||
            !string.Equals(currentUser.UserRole, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ nhân viên soát vé mới được đồng bộ check-in.");
        }

        var now = dateTime.Now;
        var results = new List<CheckInEventSyncResultResponse>(request.Events.Count);

        foreach (var evt in request.Events)
        {
            try
            {
                var (outcome, wasAlreadySynced) = await processor.ProcessAsync(
                    staffId, evt.TicketId, evt.TripStopCallId, evt.ClientEventId,
                    evt.OccurredAtDevice, evt.Note, now, isOfflineSync: true, cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                results.Add(new CheckInEventSyncResultResponse(evt.ClientEventId, outcome.Outcome, wasAlreadySynced, null));
            }
            catch (NotFoundException ex)
            {
                // Vé/điểm dừng không tồn tại (dữ liệu offline đã cũ) - không làm fail cả lô,
                // chỉ báo lỗi cho đúng event này để client biết cần preload lại manifest.
                // Dọn entity dở dang (nếu có) để không lẫn vào lần lưu của event kế tiếp.
                unitOfWork.DiscardTrackedChanges();
                results.Add(new CheckInEventSyncResultResponse(evt.ClientEventId, "Error", false, ex.Message));
            }
            catch (ConcurrencyException)
            {
                unitOfWork.DiscardTrackedChanges();
                results.Add(new CheckInEventSyncResultResponse(evt.ClientEventId, "Error", false,
                    "Vé vừa được cập nhật bởi người khác. Vui lòng đồng bộ lại."));
            }
            catch (ConflictException)
            {
                // CHK-02: 2 batch sync đồng thời cùng gửi ClientEventId chưa từng thấy ở server -
                // cả 2 đều pass bước FindEventByClientIdAsync (chưa commit), rồi cùng insert; 1
                // thắng, bên thua vi phạm unique index ClientEventId ở DB (EfUnitOfWork đã map
                // thành ConflictException). Đọc lại bản ghi vừa commit của bên thắng và trả đúng
                // outcome đó cho CHÍNH event này - không để 1 event đụng độ làm hỏng cả batch
                // (BR-OFF-03), và cũng không âm thầm báo "Error" trong khi check-in thật đã thành công.
                // Dùng lại ĐÚNG quy tắc kiểm tra identity của CHK-06 (ResolveExistingEventOutcomeAsync):
                // nếu event của request thua mang ticket/staff KHÁC bản ghi đã thắng, đây là
                // ClientEventId trùng ngẫu nhiên giữa 2 request KHÁC NHAU chứ không phải idempotent
                // replay - không được trả thẳng outcome của bản ghi kia.
                unitOfWork.DiscardTrackedChanges();
                var existing = await checkIns.FindEventByClientIdAsync(evt.ClientEventId, cancellationToken);
                if (existing is null)
                {
                    results.Add(new CheckInEventSyncResultResponse(evt.ClientEventId, "Error", false,
                        "Trùng dữ liệu do xử lý đồng thời. Vui lòng đồng bộ lại."));
                }
                else
                {
                    var (outcome, wasAlreadySynced) = await processor.ResolveExistingEventOutcomeAsync(
                        existing, staffId, evt.TicketId, evt.TripStopCallId, cancellationToken);
                    results.Add(new CheckInEventSyncResultResponse(evt.ClientEventId, outcome.Outcome, wasAlreadySynced, null));
                }
            }
        }

        return new SyncCheckInEventsResponse(results);
    }

    // ---------------- GetMyScannerAssignments ----------------

    public async Task<IReadOnlyList<MyScannerAssignmentResponse>> GetMyScannerAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(currentUser.UserId, out var staffId) ||
            !string.Equals(currentUser.UserRole, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ nhân viên soát vé mới xem được phân công của mình.");
        }

        var now = dateTime.Now;

        return await checkIns.ListMyAssignmentsAsync(staffId, now, cancellationToken);
    }

    // ---------------- GetScannerManifest ----------------

    public async Task<ScannerManifestResponse> GetScannerManifestAsync(Guid tripStopCallId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(currentUser.UserId, out var staffId) ||
            !string.Equals(currentUser.UserRole, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ nhân viên soát vé mới được tải manifest.");
        }

        var now = dateTime.Now;

        var stopCall = await checkIns.GetStopCallForManifestAsync(tripStopCallId, cancellationToken);

        // BR-BOARD-03: không trả manifest cho điểm dừng mà Staff không được phân công.
        var hasAssignment = await checkIns.HasActiveAssignmentAsync(staffId, tripStopCallId, now, cancellationToken);
        if (!hasAssignment)
        {
            throw new ForbiddenAccessException("Bạn không được phân công cho điểm dừng này.");
        }

        var trip = stopCall.Trip ?? throw new NotFoundException(nameof(Trip), stopCall.TripId);
        var route = trip.Route ?? throw new NotFoundException(nameof(Domain.Entities.Route), trip.RouteId);
        var station = stopCall.RouteStop?.Station
            ?? throw new NotFoundException(nameof(Station), stopCall.RouteStopId);

        var tripResponse = new ManifestTripResponse(
            trip.Id,
            route.Name,
            trip.Boat?.Name ?? "",
            route.DepartureStation?.Name ?? "",
            route.ArrivalStation?.Name ?? "",
            trip.DepartureTime,
            trip.ArrivalTime,
            trip.SalesCloseAt,
            trip.TripType.ToString(),
            trip.Status.ToString());

        // Chỉ các Ticket có bến lên (BoardingCallId) đúng điểm dừng này mới thuộc manifest của bến này
        // (BR-BOARD-03: Staff không check-in Ticket của bến khác dù khách đứng cùng khu vực).
        var tickets = await checkIns.ListTicketsForManifestAsync(tripStopCallId, cancellationToken);

        var bookings = tickets
            .GroupBy(t => t.SeatReservation!.BookingId)
            .Select(g =>
            {
                var booking = g.First().SeatReservation!.Booking!;
                return new ManifestBookingResponse(
                    booking.Id,
                    booking.PublicBookingId,
                    booking.BookingCode,
                    booking.TripId,
                    booking.CustomerName,
                    booking.CustomerPhone,
                    booking.QuotedTotal,
                    booking.QrCredentialVersion,
                    booking.Status.ToString(),
                    g.Select(t => new ManifestTicketResponse(
                        t.Id,
                        t.TicketCode,
                        t.SeatReservation!.PassengerName,
                        t.SeatReservation.SeatCodeAtSale ?? "",
                        t.SeatReservation.SeatClassAtSale ?? "",
                        t.BoardingStatus.ToString(),
                        t.PaidFareSnapshot))
                        .ToList());
            })
            .ToList();

        return new ScannerManifestResponse(
            stopCall.Id, station.Name, stopCall.CheckInOpenAt, stopCall.CheckInCloseAt, tripResponse, bookings, now);
    }

    // ---------------- RunNoShowFinalization ----------------

    public async Task<NoShowFinalizationResultResponse> RunNoShowFinalizationAsync(CancellationToken cancellationToken = default)
    {
        var options = noShowOptions.Value;
        var now = dateTime.Now;
        var deadline = now.AddMinutes(-options.GraceMinutes);

        // Điểm dừng đã đóng cửa sổ check-in (+ thời gian chờ đối soát) từ trước deadline.
        // BR-RF-06: bỏ qua điểm dừng của Trip đã Cancelled/Terminated - chuyến hủy/chấm dứt trước khi
        // phục vụ bến lên nghĩa là khách chưa từng có cơ hội check-in, không phải NoShow.
        var closedStopCallIds = await checkIns.ListClosedStopCallIdsAsync(deadline, cancellationToken);

        if (closedStopCallIds.Count == 0)
        {
            return new NoShowFinalizationResultResponse(0, 0, 0);
        }

        // BR-OFF-04: trì hoãn các điểm dừng còn CheckInEvent chưa giải quyết (đồng bộ offline đang dở).
        var unresolvedStopCallIds = await checkIns.ListUnresolvedStopCallIdsAsync(closedStopCallIds, cancellationToken);

        var eligibleStopCallIds = closedStopCallIds.Except(unresolvedStopCallIds).ToList();
        if (eligibleStopCallIds.Count == 0)
        {
            logger.LogInformation(
                "[NOSHOW] {Closed} điểm dừng đã đóng cửa sổ check-in nhưng đều còn sự kiện offline chưa đồng bộ, chưa kết luận NoShow.",
                closedStopCallIds.Count);
            return new NoShowFinalizationResultResponse(0, closedStopCallIds.Count, 0);
        }

        var tickets = await checkIns.ListNoShowCandidateTicketsAsync(eligibleStopCallIds, cancellationToken);

        foreach (var ticket in tickets)
        {
            ticket.BoardingStatus = BoardingStatus.NoShow;
            ticket.NoShowFinalizedAt = now;
        }

        if (tickets.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "[NOSHOW] Kết luận NoShow cho {Tickets} vé tại {StopCalls} điểm dừng; trì hoãn {Deferred} điểm dừng còn dữ liệu offline chưa đối soát.",
            tickets.Count, eligibleStopCallIds.Count, unresolvedStopCallIds.Count);

        return new NoShowFinalizationResultResponse(eligibleStopCallIds.Count, unresolvedStopCallIds.Count, tickets.Count);
    }
}
