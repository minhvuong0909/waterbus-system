using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Domain.Rules;

namespace WaterbusSystem.Application.Features.Trips.Commands.ChangeTripStatus;

/// <summary>Chuyến sau thao tác, đủ để màn hình vận hành cập nhật mà không phải tải lại danh sách.</summary>
public record TripOperationResultDto(
    Guid TripId,
    Guid? BoatId,
    string Status,
    string PreviousStatus,
    string Operation,
    DateTimeOffset? ActualDepartureTime,
    DateTimeOffset? ActualArrivalTime,
    IReadOnlyList<string> AllowedOperations);

/// <summary>
/// Đổi trạng thái chuyến (BR-OPS-01..04). Thuyền trưởng chỉ làm Start/Arrive/OpenBoarding trên chuyến của tàu mình;
/// Admin quyết định Suspend/Resume/Cancel/Terminate/Complete và quyết định cho từng chuyến của một Incident
/// (BR-INC-03: Incident không tự đổi trạng thái chuyến).
/// Mỗi lần đổi ghi một TripOperationEvent; có IncidentId thì cập nhật IncidentTrip.OperationDecision.
/// </summary>
public record ChangeTripStatusCommand(
    Guid TripId,
    TripOperation Operation,
    string? Reason,
    Guid? IncidentId) : IRequest<TripOperationResultDto>;

public class ChangeTripStatusCommandValidator : AbstractValidator<ChangeTripStatusCommand>
{
    public ChangeTripStatusCommandValidator()
    {
        RuleFor(x => x.TripId).NotEmpty().WithMessage("Thiếu mã chuyến.");
        RuleFor(x => x.Operation).IsInEnum().WithMessage("Thao tác không hợp lệ.");

        RuleFor(x => x.Reason)
            .NotEmpty().When(x => TripLifecycle.RequiresReason(x.Operation))
            .WithMessage("Cần nhập lý do cho thao tác này.");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");

        RuleFor(x => x.IncidentId)
            .NotNull().When(x => x.Operation == TripOperation.NoAction)
            .WithMessage("Quyết định \"không tác động\" phải gắn với một sự cố.");
    }
}

public class ChangeTripStatusCommandHandler : IRequestHandler<ChangeTripStatusCommand, TripOperationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;
    private readonly IOperationsAlertPublisher _publisher;

    public ChangeTripStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeService dateTime,
        IOperationsAlertPublisher publisher)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _publisher = publisher;
    }

    public async Task<TripOperationResultDto> Handle(ChangeTripStatusCommand request, CancellationToken cancellationToken)
    {
        var isAdmin = string.Equals(_currentUser.UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        var isCaptain = string.Equals(_currentUser.UserRole, "Captain", StringComparison.OrdinalIgnoreCase);
        var userId = Guid.TryParse(_currentUser.UserId, out var parsed) ? parsed : (Guid?)null;
        if (userId is null || !(isAdmin || isCaptain))
        {
            throw new ForbiddenAccessException("Chỉ Admin hoặc thuyền trưởng mới được đổi trạng thái chuyến.");
        }

        var trip = await _context.Trips
            .Include(t => t.Boat)
            .FirstOrDefaultAsync(t => t.Id == request.TripId && !t.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), request.TripId);

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

        var decision = await FindIncidentDecisionAsync(request, cancellationToken);
        var now = _dateTime.Now;
        var previous = trip.Status;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (request.Operation == TripOperation.NoAction)
        {
            RecordDecision(decision!, request.Operation, userId.Value, now);
            await SaveAsync(cancellationToken);
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
        }
        if (request.Operation == TripOperation.Arrive) trip.ActualArrivalTime = now;

        _context.TripOperationEvents.Add(new TripOperationEvent
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

        await SaveAsync(cancellationToken);

        // Chỉ phát sau khi đã lưu thành công
        await _publisher.PublishTripStatusChangedAsync(
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

    private async Task<IncidentDecision?> FindIncidentDecisionAsync(ChangeTripStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.IncidentId is not { } incidentId) return null;

        var incident = await _context.Incidents
            .Include(i => i.AffectedTrips)
            .FirstOrDefaultAsync(i => i.Id == incidentId && !i.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var link = incident.AffectedTrips.FirstOrDefault(t => t.TripId == request.TripId);
        if (link is null)
        {
            // Admin có thể xử lý một chuyến mà job chưa liên kết (vd chuyến mới sinh sau lượt quét)
            link = new IncidentTrip { IncidentId = incidentId, TripId = request.TripId };
            _context.IncidentTrips.Add(link);
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

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Người khác vừa đổi trạng thái chuyến: để người dùng tải lại rồi quyết định lại
            throw new ConcurrencyException("Trạng thái chuyến vừa được thay đổi bởi người khác. Vui lòng tải lại.", ex);
        }
    }

    private static TripOperationResultDto ToResult(Trip trip, TripStatus previous, TripOperation operation) =>
        new(trip.Id, trip.BoatId, trip.Status.ToString(), previous.ToString(), operation.ToString(),
            trip.ActualDepartureTime, trip.ActualArrivalTime,
            TripLifecycle.AllowedOperations(trip).Select(o => o.ToString()).ToList());
}
