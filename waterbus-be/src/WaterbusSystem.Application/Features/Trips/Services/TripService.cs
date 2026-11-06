using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.DTOs.Responses.Trips;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Domain.Rules;

namespace WaterbusSystem.Application.Features.Trips.Services;

/// <summary>
/// Tra cứu, tìm kiếm và vận hành chuyến tàu (BR-OPS-01..04, BR-INC-03/04).
/// Admin quyết định Suspend/Resume/Cancel/Terminate/Complete; thuyền trưởng chỉ OpenBoarding/Start/Arrive
/// trên chuyến của tàu mình. Incident không tự đổi Trip.Status (BR-INC-03).
/// </summary>
public sealed class TripService(
    ITripRepository tripRepository,
    IIncidentRepository incidentRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeService dateTime,
    IOperationsAlertPublisher publisher,
    IUserDirectory users,
    INotificationOutbox notifications,
    ICommercialTransactionRunner transactions) : ITripService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    // ---------------- GetDetail ----------------

    public async Task<TripDetailResponse> GetDetailAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await tripRepository.GetDetailGraphAsync(tripId, cancellationToken);
        var salesState = TripReadRules.SalesState(trip, dateTime.Now);
        return new(trip.Id, trip.RouteId, trip.Route?.Name ?? "", trip.TripType.ToString(),
            trip.Status.ToString(), trip.DepartureTime, trip.ArrivalTime, trip.SalesCloseAt,
            trip.IsDelayed, salesState == "open", salesState,
            trip.Boat is null ? null : new(trip.Boat.Id, trip.Boat.Code, trip.Boat.Name),
            trip.TripStopCalls.OrderBy(c => c.VisitOrder).Select(c => new TripCallResponse(
                c.Id, c.RouteStop!.StationId, c.RouteStop.Station!.Name, c.VisitOrder,
                c.RouteStop.CanBoard && c.Status != "Skipped", c.RouteStop.CanAlight && c.Status != "Skipped",
                c.Status, c.EstimatedArrivalTime, c.EstimatedDepartureTime, c.CheckInOpenAt, c.CheckInCloseAt)).ToList());
    }

    // ---------------- GetSeatMap ----------------

    public async Task<TripSeatMapResponse> GetSeatMapAsync(Guid tripId, GetTripSeatMapRequest request, CancellationToken cancellationToken = default)
    {
        var trip = await tripRepository.GetDetailGraphAsync(tripId, cancellationToken);
        var boarding = trip.TripStopCalls.SingleOrDefault(c => c.Id == request.BoardingCallId);
        var disembarking = trip.TripStopCalls.SingleOrDefault(c => c.Id == request.DisembarkingCallId);
        if (boarding is null || disembarking is null)
            throw new ValidationException([new("boardingCallId", "Điểm lên/xuống phải thuộc hành trình thực sự của chuyến.")]);
        // Dùng cùng quy tắc chọn hành trình với checkout, gồm chuyến vòng cùng Station.
        BookingCreationService.ResolveJourneyCalls(trip, boarding.RouteStop!.StationId,
            disembarking.RouteStop!.StationId, boarding.Id, disembarking.Id);
        if (trip.Boat is null) throw new ConcurrencyException("Chuyến chưa có tàu và sơ đồ ghế hợp lệ.");

        var now = dateTime.Now;
        var salesState = TripReadRules.SalesState(trip, now);
        var seats = await tripRepository.ListSeatsForBoatAsync(trip.Boat.Id, cancellationToken);
        var type = trip.TripType.ToString();
        var fares = await tripRepository.ListActiveFaresForTypeAsync(type, now, cancellationToken);
        if (fares.GroupBy(f => f.SeatClassId).Any(g => g.Count() > 1))
            throw new ConcurrencyException("Có nhiều bảng giá cùng hiệu lực; cần Admin kiểm tra trước khi bán.");
        var priceByClass = fares.ToDictionary(f => f.SeatClassId);
        var overlaps = await tripRepository.ListOverlappingReservationsAsync(trip.Id, boarding.VisitOrder, disembarking.VisitOrder, cancellationToken);
        var bySeat = overlaps.ToLookup(r => r.SeatId);

        var result = seats.Select(seat =>
        {
            priceByClass.TryGetValue(seat.SeatClassId, out var fare);
            var reservations = bySeat[seat.Id].ToList();
            var reason = salesState != "open" ? salesState : TripReadRules.SeatBlockReason(seat, fare);
            var status = reason != null ? "unavailable"
                : reservations.Any(r => r.Status != ReservationStatus.Pending) ? "sold"
                : reservations.Count > 0 ? "held" : "available";
            return new TripSeatResponse(seat.Id, seat.SeatCode, seat.RowNumber, seat.ColumnNumber,
                seat.SeatClassId, seat.SeatClass?.Name, fare?.Price, fare?.Currency ?? "VND", status, reason,
                status == "held" ? reservations.Max(r => r.ExpiresAt) : null);
        }).ToList();
        return new(trip.Id, trip.Boat.Id, boarding.Id, disembarking.Id, boarding.VisitOrder,
            disembarking.VisitOrder, salesState == "open" && result.Any(s => s.Status == "available"),
            salesState, result.Count(s => s.Status == "available"), now, result);
    }

    // ---------------- GetOperations ----------------

    private const int MaxOperationsItems = 200;

    public async Task<List<TripOperationItemResponse>> GetOperationsAsync(GetTripOperationsRequest request, CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUser.UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        var isCaptain = string.Equals(currentUser.UserRole, "Captain", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !(isCaptain && Guid.TryParse(currentUser.UserId, out _)))
        {
            throw new ForbiddenAccessException("Chỉ Admin hoặc thuyền trưởng mới được xem danh sách chuyến vận hành.");
        }

        var now = dateTime.Now;
        var from = request.From ?? now.AddHours(-12);
        var to = request.To ?? from.AddDays(7);
        Guid? captainId = isAdmin ? null : Guid.Parse(currentUser.UserId!);

        var rows = await tripRepository.ListForOperationsAsync(from, to, captainId, request.Statuses, MaxOperationsItems, cancellationToken);

        return rows.Select(t => new TripOperationItemResponse(
                t.Id, t.ScheduleId, t.RouteId, t.Route!.Code, t.Route.Name, t.BoatId, t.Boat?.Code,
                t.TripType.ToString(), t.Status.ToString(), t.DepartureTime, t.ArrivalTime,
                t.ActualDepartureTime, t.ActualArrivalTime, t.IsDelayed,
                TripLifecycle.AllowedOperations(t).Select(o => o.ToString()).ToList()))
            .ToList();
    }

    // ---------------- GetOperationEvents ----------------

    public async Task<List<TripOperationEventResponse>> GetOperationEventsAsync(GetTripOperationEventsRequest request, CancellationToken cancellationToken = default)
    {
        var events = await tripRepository.ListOperationEventsAsync(request.TripId, request.Take, cancellationToken);

        var actorIds = events
            .Select(e => e.ActedByAdminId ?? e.ActedByCaptainId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var names = await users.GetFullNamesAsync(actorIds, cancellationToken);

        return events.Select(e =>
        {
            var actor = e.ActedByAdminId ?? e.ActedByCaptainId;
            return new TripOperationEventResponse(
                e.Id, e.TripId, e.IncidentId, e.Action, e.FromStatus, e.ToStatus, e.Note, e.OccurredAt,
                actor is { } id && names.TryGetValue(id, out var name) ? name : null);
        }).ToList();
    }

    // ---------------- ChangeStatus ----------------

    public async Task<TripOperationResultResponse> ChangeStatusAsync(Guid tripId, ChangeTripStatusRequest request, CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUser.UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        var isCaptain = string.Equals(currentUser.UserRole, "Captain", StringComparison.OrdinalIgnoreCase);
        var userId = Guid.TryParse(currentUser.UserId, out var parsed) ? parsed : (Guid?)null;
        if (userId is null || !(isAdmin || isCaptain))
        {
            throw new ForbiddenAccessException("Chỉ Admin hoặc thuyền trưởng mới được đổi trạng thái chuyến.");
        }

        var trip = await tripRepository.GetTrackedWithBoatAsync(tripId, cancellationToken);

        if (!isAdmin)
        {
            if (!TripLifecycle.IsCaptainOperation(request.Operation))
            {
                throw new ForbiddenAccessException("Thuyền trưởng chỉ được mở đón khách, rời bến và cập bến. Các quyết định khác do Admin thực hiện.");
            }
            if (request.IncidentId is not null)
            {
                throw new ForbiddenAccessException("Chỉ Admin được gắn quyết định với sự cố.");
            }
            if (trip.Boat?.CaptainUserId != userId)
            {
                throw new ForbiddenAccessException("Bạn không phải thuyền trưởng của tàu chạy chuyến này.");
            }
        }

        var decision = await FindIncidentDecisionAsync(tripId, request.IncidentId, cancellationToken);
        var now = dateTime.Now;
        var previous = trip.Status;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (request.Operation == TripOperation.NoAction)
        {
            RecordDecision(decision!, request.Operation, userId.Value, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ToResult(trip, previous, request.Operation);
        }

        var next = TripLifecycle.NextStatus(trip, request.Operation)
            ?? throw new ConflictException(
                $"Không thể thực hiện \"{request.Operation}\" khi chuyến đang ở trạng thái {trip.Status}.");

        if (request.Operation == TripOperation.Start && trip.BoatId is null)
        {
            throw new ConflictException("Chuyến chưa được gán tàu nên không thể xuất bến.");
        }

        trip.Status = next;
        trip.UpdatedAt = now;
        if (request.Operation == TripOperation.Start)
        {
            trip.ActualDepartureTime = now;
            CloseSales(trip, now);
            // BR-STOP-02: rời bến đầu nghĩa là điểm dừng đầu tiên (chưa Skipped) đã được phục vụ xong.
            MarkStopDeparted(FirstServedStop(trip), now);
        }
        if (request.Operation == TripOperation.Arrive)
        {
            trip.ActualArrivalTime = now;
            // BR-STOP-02: cập bến cuối nghĩa là điểm dừng cuối cùng (chưa Skipped) đã được phục vụ xong.
            MarkStopArrived(LastServedStop(trip), now);
        }

        tripRepository.AddOperationEvent(new TripOperationEvent
        {
            TripId = trip.Id,
            IncidentId = request.IncidentId,
            ActedByAdminId = isAdmin ? userId : null,
            ActedByCaptainId = isCaptain && !isAdmin ? userId : null,
            Action = request.Operation.ToString(),
            FromStatus = previous.ToString(),
            ToStatus = next.ToString(),
            OccurredAt = now,
            Note = reason
        });

        if (decision is not null)
        {
            RecordDecision(decision, request.Operation, userId.Value, now);
        }

        if (next is TripStatus.Cancelled or TripStatus.Terminated)
        {
            // Cùng khoá thương mại với xác nhận thanh toán: IPN không thể xác nhận vé giữa lúc huỷ chuyến và tạo
            // thông báo. Outbox được ghi trong cùng transaction với việc đổi trạng thái (commit xong mới có email).
            var type = next == TripStatus.Cancelled ? EmailNotificationType.TripCancelled : EmailNotificationType.TripTerminated;
            await transactions.RunAsync(async () =>
            {
                await notifications.EnqueueTripDisruptionAsync(trip.Id, type, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }
        else
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Chỉ phát sau khi đã lưu thành công
        await publisher.PublishTripStatusChangedAsync(
            new TripStatusChangedMessage(trip.Id, trip.BoatId, next.ToString(), previous.ToString(), request.Operation.ToString(), now),
            cancellationToken);

        return ToResult(trip, previous, request.Operation);
    }

    /// <summary>
    /// Xuất bến là đóng bán vé (BR-TIME-02): giờ đóng bán không được muộn hơn giờ khởi hành thực tế, cũng không muộn hơn
    /// giờ khởi hành dự kiến (ràng buộc CK_Trips_SalesClose). Thuyền trưởng có thể rời bến sớm hơn giờ đóng bán đã đặt.
    /// </summary>
    private static void CloseSales(Trip trip, DateTimeOffset departedAt)
    {
        var latest = departedAt < trip.DepartureTime ? departedAt : trip.DepartureTime;
        if (trip.SalesCloseAt is null || trip.SalesCloseAt > latest)
        {
            trip.SalesCloseAt = latest;
        }
    }

    /// <summary>Sự cố và liên kết sự cố - chuyến (tạo mới nếu chưa có) mà Admin đang ra quyết định.</summary>
    private sealed record IncidentDecision(Incident Incident, IncidentTrip Link);

    private async Task<IncidentDecision?> FindIncidentDecisionAsync(Guid tripId, Guid? incidentId, CancellationToken cancellationToken)
    {
        if (incidentId is not { } id) return null;

        var incident = await incidentRepository.GetRequiredWithAffectedTripsAsync(id, cancellationToken);

        var link = incident.AffectedTrips.FirstOrDefault(t => t.TripId == tripId);
        if (link is null)
        {
            // Admin có thể xử lý một chuyến mà job chưa liên kết (vd chuyến mới sinh sau lượt quét)
            link = new IncidentTrip { IncidentId = id, TripId = tripId };
            incidentRepository.AddTripLink(link);
        }

        return new IncidentDecision(incident, link);
    }

    private static void RecordDecision(IncidentDecision decision, TripOperation operation, Guid adminId, DateTimeOffset now)
    {
        decision.Link.OperationDecision = TripLifecycle.DecisionOf(operation);
        decision.Link.DecisionByAdminId = adminId;
        decision.Link.DecisionAt = now;
        decision.Incident.HandledByAdminId = adminId;
    }

    private static TripOperationResultResponse ToResult(Trip trip, TripStatus previous, TripOperation operation) =>
        new(trip.Id, trip.BoatId, trip.Status.ToString(), previous.ToString(), operation.ToString(),
            trip.ActualDepartureTime, trip.ActualArrivalTime,
            TripLifecycle.AllowedOperations(trip).Select(o => o.ToString()).ToList());

    private static TripStopCall? FirstServedStop(Trip trip) =>
        trip.TripStopCalls.Where(c => c.Status != "Skipped").MinBy(c => c.VisitOrder);

    private static TripStopCall? LastServedStop(Trip trip) =>
        trip.TripStopCalls.Where(c => c.Status != "Skipped").MaxBy(c => c.VisitOrder);

    private static void MarkStopDeparted(TripStopCall? call, DateTimeOffset now)
    {
        if (call is null) return;
        call.ActualArrivalTime ??= now;
        call.ActualDepartureTime = now;
        call.Status = "Departed";
    }

    private static void MarkStopArrived(TripStopCall? call, DateTimeOffset now)
    {
        if (call is null) return;
        call.ActualArrivalTime = now;
        call.Status = "Arrived";
    }

    // ---------------- RecordStopProgress ----------------

    /// <summary>
    /// Ghi nhận tiến độ phục vụ thực tế tại 1 điểm dừng trung gian (BR-STOP-02) - KHÔNG đổi Trip.Status
    /// (BR-OPS-02: tình trạng cập/rời từng bến được theo dõi riêng, không lặp EnRoute → Boarding → EnRoute).
    /// Đây là dữ liệu mà RefundEligibilityEvaluator dùng để xác định đoạn đi nào đã thực sự được phục vụ
    /// trước khi Trip bị Terminate giữa đường (BR-RF-02/03) - điểm dừng đầu/cuối tự ghi theo Start/Arrive.
    /// </summary>
    public async Task<StopProgressResultResponse> RecordStopProgressAsync(
        Guid tripId, Guid tripStopCallId, RecordStopProgressRequest request, CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUser.UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        var isCaptain = string.Equals(currentUser.UserRole, "Captain", StringComparison.OrdinalIgnoreCase);
        var userId = Guid.TryParse(currentUser.UserId, out var parsed) ? parsed : (Guid?)null;
        if (userId is null || !(isAdmin || isCaptain))
        {
            throw new ForbiddenAccessException("Chỉ Admin hoặc thuyền trưởng mới được ghi nhận tiến độ điểm dừng.");
        }

        var stopCall = await tripRepository.GetTrackedStopCallAsync(tripId, tripStopCallId, cancellationToken);
        var trip = stopCall.Trip ?? throw new NotFoundException(nameof(Trip), tripId);

        if (!isAdmin && trip.Boat?.CaptainUserId != userId)
        {
            throw new ForbiddenAccessException("Bạn không phải thuyền trưởng của tàu chạy chuyến này.");
        }

        if (!TripLifecycle.CanRecordStopProgress(trip.Status))
        {
            throw new ConflictException(
                $"Không thể ghi nhận tiến độ điểm dừng khi chuyến đang ở trạng thái {trip.Status}.");
        }

        if (stopCall.Status == "Skipped")
        {
            throw new ConflictException("Điểm dừng này đã bị bỏ qua, không thể ghi nhận tiến độ.");
        }

        // BR-STOP-02: endpoint này chỉ dành cho điểm dừng TRUNG GIAN - điểm dừng đầu/cuối đã tự ghi theo
        // Start/Arrive (ChangeStatusAsync). Cho phép ghi đè điểm dừng cuối ở đây sẽ đánh dấu hành trình đã
        // hoàn tất dù Trip còn EnRoute, khiến RefundEligibilityEvaluator từ chối hoàn tiền sai nếu sau đó
        // Trip bị Terminate trước khi thực sự tới bến cuối.
        var served = trip.TripStopCalls.Where(c => c.Status != "Skipped" || c.Id == stopCall.Id)
            .OrderBy(c => c.VisitOrder).ToList();
        var index = served.FindIndex(c => c.Id == stopCall.Id);
        if (index <= 0 || index >= served.Count - 1)
        {
            throw new ConflictException(
                "Chỉ dùng endpoint này cho điểm dừng trung gian; điểm dừng đầu/cuối ghi nhận qua thao tác Start/Arrive.");
        }

        // BR-STOP-02: không cho ghi nhận vượt trước - điểm dừng ngay trước phải đã Departed thì mới tới
        // lượt điểm dừng này (điểm dừng đầu đã Departed ngay khi Start, nên index 1 luôn thoả sẵn).
        var previous = served[index - 1];
        if (previous.Status != "Departed")
        {
            throw new ConflictException(
                $"Điểm dừng trước đó (VisitOrder {previous.VisitOrder}) chưa rời bến, không thể ghi nhận tiến độ điểm dừng này.");
        }

        var now = dateTime.Now;

        switch (request.Action)
        {
            case StopCallProgress.Arrived:
                if (stopCall.Status != "Planned")
                {
                    throw new ConflictException($"Điểm dừng đang ở trạng thái {stopCall.Status}, không thể đánh dấu đã đến.");
                }
                stopCall.ActualArrivalTime = now;
                stopCall.Status = "Arrived";
                break;

            case StopCallProgress.Departed:
                if (stopCall.Status is not ("Planned" or "Arrived"))
                {
                    throw new ConflictException($"Điểm dừng đang ở trạng thái {stopCall.Status}, không thể đánh dấu đã rời.");
                }
                stopCall.ActualArrivalTime ??= now;
                stopCall.ActualDepartureTime = now;
                stopCall.Status = "Departed";
                break;

            default:
                throw new ValidationException([new("action", "Thao tác không hợp lệ.")]);
        }

        tripRepository.AddOperationEvent(new TripOperationEvent
        {
            TripId = trip.Id,
            ActedByAdminId = isAdmin ? userId : null,
            ActedByCaptainId = isCaptain && !isAdmin ? userId : null,
            Action = $"Stop{request.Action}",
            FromStatus = trip.Status.ToString(),
            ToStatus = trip.Status.ToString(),
            OccurredAt = now,
            Note = $"TripStopCallId={stopCall.Id}, VisitOrder={stopCall.VisitOrder}"
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new StopProgressResultResponse(stopCall.Id, stopCall.Status, stopCall.ActualArrivalTime, stopCall.ActualDepartureTime);
    }

    // ---------------- ReportSos ----------------

    public async Task<SosReportResponse> ReportSosAsync(Guid tripId, ReportTripSosRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(currentUser.UserId, out var captainId)
            || !string.Equals(currentUser.UserRole, "Captain", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ thuyền trưởng mới được báo SOS.");
        }

        var trip = await tripRepository.GetWithBoatAsync(tripId, cancellationToken);

        if (trip.Boat?.CaptainUserId != captainId)
        {
            throw new ForbiddenAccessException("Bạn không phải thuyền trưởng của tàu chạy chuyến này.");
        }

        var now = dateTime.Now;
        var note = string.IsNullOrWhiteSpace(request.Note) ? "Thuyền trưởng báo khẩn cấp." : request.Note.Trim();

        var incident = new Incident
        {
            ScopeType = IncidentConstants.Scopes.Trip,
            BoatId = trip.BoatId,
            RouteId = trip.RouteId,
            ReportedByCaptainId = captainId,
            Source = IncidentConstants.Sources.Captain,
            IncidentType = IncidentConstants.Types.Safety,
            Severity = IncidentConstants.Severities.High,
            Status = IncidentConstants.Statuses.Open,
            Description = note,
            OccurredAt = now
        };
        incidentRepository.Add(incident);
        // Admin quyết định hành động cho chuyến này sau (OperationDecision để trống)
        incidentRepository.AddTripLink(new IncidentTrip { IncidentId = incident.Id, TripId = trip.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await publisher.PublishIncidentRaisedAsync(
            new IncidentRaisedMessage(incident.Id, incident.IncidentType, incident.Severity, trip.Id, trip.BoatId, note, now),
            cancellationToken);

        return new SosReportResponse(incident.Id, now);
    }

    // ---------------- Search ----------------

    public async Task<List<TripSearchResponse>> SearchAsync(SearchTripsRequest request, CancellationToken cancellationToken = default)
    {
        DateTimeOffset? dayStart = null;
        DateTimeOffset? dayEnd = null;
        if (request.DepartureDate.HasValue)
        {
            // Ngày khách chọn là ngày giờ Việt Nam (UTC+7); DepartureTime lưu UTC nên so theo khoảng [00:00, 24:00) giờ VN
            var date = request.DepartureDate.Value.Date;
            dayStart = new DateTimeOffset(date, VietnamOffset);
            dayEnd = dayStart.Value.AddDays(1);
        }

        // BR-TIME-01/02: bán được khi Scheduled/Boarding, chưa xuất bến, chưa quá SalesCloseAt (cùng quy tắc Seat Map/Checkout).
        var now = dateTime.Now;
        var candidates = await tripRepository.SearchSellableAsync(dayStart, dayEnd, request.TripType, now, cancellationToken);

        // H05: bến lên/bến xuống hợp lệ của một Trip là các điểm dừng THỰC TẾ của chuyến (TripStopCall →
        // RouteStop), theo VisitOrder; chuyến vòng (lặp bến) lấy lần lên đầu / lần xuống cuối như checkout.
        var matched = new List<(Trip Trip, List<TripStopCall> Calls, TripStopCall Boarding, TripStopCall Disembarking)>();
        foreach (var trip in candidates)
        {
            if (TripReadRules.SalesState(trip, now) != "open") continue;

            var calls = trip.TripStopCalls
                .Where(c => c.RouteStop != null && c.Status != "Skipped")
                .OrderBy(c => c.VisitOrder)
                .ToList();
            if (calls.Count < 2) continue;

            var boardingCall = request.DepartureStationId.HasValue
                ? calls.FirstOrDefault(c => c.RouteStop!.StationId == request.DepartureStationId.Value && c.RouteStop.CanBoard)
                : calls.FirstOrDefault(c => c.RouteStop!.CanBoard);
            if (boardingCall == null) continue;

            var disembarkingCall = request.ArrivalStationId.HasValue
                ? calls.LastOrDefault(c => c.RouteStop!.StationId == request.ArrivalStationId.Value && c.RouteStop.CanAlight)
                : calls.LastOrDefault(c => c.RouteStop!.CanAlight);
            if (disembarkingCall == null || boardingCall.VisitOrder >= disembarkingCall.VisitOrder) continue;

            matched.Add((trip, calls, boardingCall, disembarkingCall));
        }

        if (matched.Count == 0) return [];

        // Tồn kho thật: Seat đang hoạt động của đúng tàu, hạng ghế hoạt động, có đúng một FareRule VND hiệu lực.
        var boatIds = matched.Select(m => m.Trip.BoatId!.Value).Distinct().ToList();
        var seatsByBoat = (await tripRepository.ListSeatsForBoatsAsync(boatIds, cancellationToken)).ToLookup(s => s.BoatId);
        var faresByType = (await tripRepository.ListActiveFaresAsync(now, cancellationToken))
            .GroupBy(f => f.TripType)
            .ToDictionary(g => g.Key, g => TripReadRules.UnambiguousFares(g));

        var reservations = (await tripRepository.ListReservationSegmentsAsync(
            matched.Select(m => m.Trip.Id).ToList(), cancellationToken)).ToLookup(r => r.TripId);

        var results = new List<TripSearchResponse>();
        foreach (var (trip, calls, boarding, disembarking) in matched)
        {
            var fares = faresByType.GetValueOrDefault(trip.TripType.ToString()) ?? [];
            var saleable = seatsByBoat[trip.BoatId!.Value]
                .Where(s => TripReadRules.SeatBlockReason(s, fares.GetValueOrDefault(s.SeatClassId)) == null)
                .ToList();
            if (saleable.Count == 0) continue; // không có ghế bán được (hoặc thiếu giá) thì không quảng cáo chuyến

            var busy = reservations[trip.Id]
                .Where(r => boarding.VisitOrder < r.DisembarkingStopOrder && disembarking.VisitOrder > r.BoardingStopOrder)
                .Select(r => r.SeatId).ToHashSet();
            var available = saleable.Where(s => !busy.Contains(s.Id)).ToList();

            // Giá "từ" lấy trên ghế còn bán được; hết ghế thì lấy trên tồn kho bán được của chuyến.
            var priced = available.Count > 0 ? available : saleable;
            var fromPrice = priced.Min(s => fares[s.SeatClassId].Price);

            results.Add(new TripSearchResponse(
                trip.Id,
                trip.Route?.Name ?? "Tuyến Buýt Đường Sông",
                boarding.RouteStop!.Station?.Name ?? "",
                disembarking.RouteStop!.Station?.Name ?? "",
                trip.DepartureTime,
                trip.ArrivalTime,
                JourneyMinutes(trip, calls, boarding, disembarking),
                trip.TripType.ToString(),
                fromPrice,
                available.Count,
                calls.Select(c => new TripSearchStopResponse(
                    c.RouteStop!.StationId, c.RouteStop.Station?.Name ?? "", c.VisitOrder)).ToList(),
                available.Count > 0,
                available.Count > 0 ? "open" : "soldOut"));
        }

        // Không gửi pageSize = trả đủ (FE/AI lọc cục bộ nên cắt ngầm sẽ làm mất chuyến); tập ứng viên đã bị giới hạn
        // bởi ngày + SalesCloseAt. ponytail: ép phân trang phía server khi FE/AI hỗ trợ nhiều trang.
        return request.PageSize is { } size ? results.Skip((request.Page - 1) * size).Take(size).ToList() : results;
    }

    /// <summary>Thời lượng đúng chặng khi có lịch từng bến; cả hành trình thì lấy giờ chuyến; còn lại null (không bịa).</summary>
    private static int? JourneyMinutes(Trip trip, List<TripStopCall> calls, TripStopCall boarding, TripStopCall disembarking)
    {
        if (boarding.EstimatedDepartureTime is { } from && disembarking.EstimatedArrivalTime is { } to && to > from)
            return (int)Math.Round((to - from).TotalMinutes);
        if (boarding.Id == calls[0].Id && disembarking.Id == calls[^1].Id && trip.ArrivalTime > trip.DepartureTime)
            return (int)Math.Round((trip.ArrivalTime - trip.DepartureTime).TotalMinutes);
        return null;
    }
}
