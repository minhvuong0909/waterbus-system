using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;
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
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeService dateTime,
    CheckInProcessor processor,
    IOptions<NoShowFinalizationOptions> noShowOptions,
    ILogger<CheckInService> logger) : ICheckInService
{
    // ---------------- AssignScanner ----------------

    public async Task<AssignScannerResponse> AssignScannerAsync(AssignScannerRequest request, CancellationToken cancellationToken = default)
    {
        var device = await context.ScannerDevices
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && !d.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(ScannerDevice), request.DeviceId);

        if (!device.IsActive)
        {
            throw new ConflictException("Thiết bị đã bị ngưng hoạt động, không thể phân công.");
        }

        var stopCall = await context.TripStopCalls
            .FirstOrDefaultAsync(c => c.Id == request.TripStopCallId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(TripStopCall), request.TripStopCallId);

        if (request.IsPrimaryOffline)
        {
            var requestedEnd = request.ActiveUntil ?? DateTimeOffset.MaxValue;

            // BR-OFF-02: chỉ 1 Primary Offline Scanner hoạt động cùng lúc cho cùng điểm dừng.
            var hasOverlappingPrimary = await context.ScannerAssignments.AsNoTracking().AnyAsync(a =>
                    !a.IsDeleted &&
                    a.TripStopCallId == request.TripStopCallId &&
                    a.IsPrimaryOffline &&
                    a.ActiveFrom < requestedEnd &&
                    (a.ActiveUntil ?? DateTimeOffset.MaxValue) > request.ActiveFrom,
                cancellationToken);

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
        context.ScannerAssignments.Add(assignment);

        await context.SaveChangesAsync(cancellationToken);

        return new AssignScannerResponse(
            assignment.Id, assignment.DeviceId, assignment.StaffAccountId, assignment.TripStopCallId,
            assignment.IsPrimaryOffline, assignment.ActiveFrom, assignment.ActiveUntil);
    }

    // ---------------- RevokeScannerAssignment ----------------

    public async Task<RevokeScannerAssignmentResponse> RevokeScannerAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await context.ScannerAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(ScannerAssignment), assignmentId);

        var now = dateTime.Now;

        if (assignment.ActiveFrom > now)
        {
            // Chưa từng có hiệu lực (phân công cho tương lai) - thu hồi hẳn, không để lại cửa sổ ảo.
            assignment.ActiveUntil = assignment.ActiveFrom;
        }
        else if (assignment.ActiveUntil is null || assignment.ActiveUntil > now)
        {
            assignment.ActiveUntil = now;
        }
        // Đã kết thúc từ trước thì giữ nguyên (thao tác idempotent).

        assignment.UpdatedAt = now;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Phân công vừa được cập nhật bởi người khác. Vui lòng tải lại.", ex);
        }

        return new RevokeScannerAssignmentResponse(assignment.Id, assignment.ActiveUntil!.Value);
    }

    // ---------------- SetCheckInWindow ----------------

    public async Task<SetCheckInWindowResponse> SetCheckInWindowAsync(Guid tripStopCallId, SetCheckInWindowRequest request, CancellationToken cancellationToken = default)
    {
        var stopCall = await context.TripStopCalls
            .FirstOrDefaultAsync(c => c.Id == tripStopCallId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(TripStopCall), tripStopCallId);

        // Điểm dừng đã khởi hành/bị bỏ qua thì không còn ý nghĩa để cấu hình cửa sổ check-in.
        if (stopCall.Status is "Departed" or "Skipped")
        {
            throw new ConflictException("Không thể đặt cửa sổ check-in cho điểm dừng đã khởi hành hoặc bị bỏ qua.");
        }

        stopCall.CheckInOpenAt = request.CheckInOpenAt;
        stopCall.CheckInCloseAt = request.CheckInCloseAt;
        stopCall.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Điểm dừng vừa được cập nhật bởi người khác. Vui lòng tải lại.", ex);
        }

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
            request.OccurredAtDevice, request.Note, now, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Vé vừa được cập nhật bởi người khác. Vui lòng quét lại.", ex);
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
                    evt.OccurredAtDevice, evt.Note, now, cancellationToken);

                await context.SaveChangesAsync(cancellationToken);

                results.Add(new CheckInEventSyncResultResponse(evt.ClientEventId, outcome.Outcome, wasAlreadySynced, null));
            }
            catch (NotFoundException ex)
            {
                // Vé/điểm dừng không tồn tại (dữ liệu offline đã cũ) - không làm fail cả lô,
                // chỉ báo lỗi cho đúng event này để client biết cần preload lại manifest.
                // Dọn entity dở dang (nếu có) để không lẫn vào lần SaveChangesAsync của event kế tiếp.
                context.DiscardTrackedChanges();
                results.Add(new CheckInEventSyncResultResponse(evt.ClientEventId, "Error", false, ex.Message));
            }
            catch (DbUpdateConcurrencyException)
            {
                context.DiscardTrackedChanges();
                results.Add(new CheckInEventSyncResultResponse(evt.ClientEventId, "Error", false,
                    "Vé vừa được cập nhật bởi người khác. Vui lòng đồng bộ lại."));
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

        return await context.ScannerAssignments.AsNoTracking()
            .Where(a => !a.IsDeleted && a.StaffAccountId == staffId &&
                        (a.ActiveUntil == null || a.ActiveUntil >= now))
            .OrderBy(a => a.ActiveFrom)
            .Select(a => new MyScannerAssignmentResponse(
                a.Id,
                a.TripStopCallId,
                a.TripStopCall!.TripId,
                a.TripStopCall.Trip!.Route!.Name,
                a.TripStopCall.RouteStop!.Station!.Name,
                a.TripStopCall.VisitOrder,
                a.TripStopCall.Trip.Status.ToString(),
                a.TripStopCall.EstimatedArrivalTime,
                a.DeviceId,
                a.Device!.DeviceCode,
                a.IsPrimaryOffline,
                a.ActiveFrom,
                a.ActiveUntil,
                a.TripStopCall.CheckInOpenAt,
                a.TripStopCall.CheckInCloseAt))
            .ToListAsync(cancellationToken);
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

        var stopCall = await context.TripStopCalls.AsNoTracking()
            .Include(c => c.RouteStop!).ThenInclude(rs => rs!.Station)
            .Include(c => c.Trip!).ThenInclude(t => t!.Route!).ThenInclude(r => r!.DepartureStation)
            .Include(c => c.Trip!).ThenInclude(t => t!.Route!).ThenInclude(r => r!.ArrivalStation)
            .Include(c => c.Trip!).ThenInclude(t => t!.Boat)
            .FirstOrDefaultAsync(c => c.Id == tripStopCallId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(TripStopCall), tripStopCallId);

        // BR-BOARD-03: không trả manifest cho điểm dừng mà Staff không được phân công.
        var hasAssignment = await context.ScannerAssignments.AsNoTracking().AnyAsync(a =>
                a.StaffAccountId == staffId &&
                a.TripStopCallId == tripStopCallId &&
                a.ActiveFrom <= now &&
                (a.ActiveUntil == null || a.ActiveUntil >= now),
            cancellationToken);
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
        var tickets = await context.Tickets.AsNoTracking()
            .Include(t => t.SeatReservation!).ThenInclude(r => r!.Booking)
            .Where(t => !t.IsDeleted && t.Status == TicketStatus.Valid &&
                        t.SeatReservation!.Booking!.BoardingCallId == tripStopCallId)
            .ToListAsync(cancellationToken);

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
        var closedStopCallIds = await context.TripStopCalls.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CheckInCloseAt != null && c.CheckInCloseAt <= deadline
                        && c.Trip!.Status != TripStatus.Cancelled && c.Trip!.Status != TripStatus.Terminated)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (closedStopCallIds.Count == 0)
        {
            return new NoShowFinalizationResultResponse(0, 0, 0);
        }

        // BR-OFF-04: trì hoãn các điểm dừng còn CheckInEvent chưa giải quyết (đồng bộ offline đang dở).
        var unresolvedStopCallIds = await context.CheckInEvents.AsNoTracking()
            .Where(e => closedStopCallIds.Contains(e.TripStopCallId) &&
                        (e.SyncStatus == "Pending" || e.SyncStatus == "Conflict"))
            .Select(e => e.TripStopCallId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var eligibleStopCallIds = closedStopCallIds.Except(unresolvedStopCallIds).ToList();
        if (eligibleStopCallIds.Count == 0)
        {
            logger.LogInformation(
                "[NOSHOW] {Closed} điểm dừng đã đóng cửa sổ check-in nhưng đều còn sự kiện offline chưa đồng bộ, chưa kết luận NoShow.",
                closedStopCallIds.Count);
            return new NoShowFinalizationResultResponse(0, closedStopCallIds.Count, 0);
        }

        var tickets = await context.Tickets
            .Where(t => !t.IsDeleted &&
                        t.Status == TicketStatus.Valid &&
                        t.BoardingStatus == BoardingStatus.NotCheckedIn &&
                        eligibleStopCallIds.Contains(t.SeatReservation!.Booking!.BoardingCallId))
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            ticket.BoardingStatus = BoardingStatus.NoShow;
            ticket.NoShowFinalizedAt = now;
        }

        if (tickets.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "[NOSHOW] Kết luận NoShow cho {Tickets} vé tại {StopCalls} điểm dừng; trì hoãn {Deferred} điểm dừng còn dữ liệu offline chưa đối soát.",
            tickets.Count, eligibleStopCallIds.Count, unresolvedStopCallIds.Count);

        return new NoShowFinalizationResultResponse(eligibleStopCallIds.Count, unresolvedStopCallIds.Count, tickets.Count);
    }
}
