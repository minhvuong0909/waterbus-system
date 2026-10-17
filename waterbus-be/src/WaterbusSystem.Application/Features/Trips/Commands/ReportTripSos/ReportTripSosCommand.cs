using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Trips.Commands.ReportTripSos;

public record SosReportDto(Guid IncidentId, DateTimeOffset ReportedAt);

/// <summary>
/// Thuyền trưởng báo khẩn cấp cho chuyến của tàu mình (BR-INC-04: Captain được báo Incident).
/// Chỉ GHI NHẬN sự cố và báo Admin; không đổi trạng thái chuyến (BR-INC-03/04: ghi nhận và quyết định là hai nghiệp vụ).
/// </summary>
public record ReportTripSosCommand(Guid TripId, string? Note) : IRequest<SosReportDto>;

public class ReportTripSosCommandValidator : AbstractValidator<ReportTripSosCommand>
{
    public ReportTripSosCommandValidator()
    {
        RuleFor(x => x.TripId).NotEmpty().WithMessage("Thiếu mã chuyến.");
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class ReportTripSosCommandHandler : IRequestHandler<ReportTripSosCommand, SosReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;
    private readonly IOperationsAlertPublisher _publisher;

    public ReportTripSosCommandHandler(
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

    public async Task<SosReportDto> Handle(ReportTripSosCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var captainId)
            || !string.Equals(_currentUser.UserRole, "Captain", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ thuyền trưởng mới được báo SOS.");
        }

        var trip = await _context.Trips.AsNoTracking()
            .Include(t => t.Boat)
            .FirstOrDefaultAsync(t => t.Id == request.TripId && !t.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), request.TripId);

        if (trip.Boat?.CaptainUserId != captainId)
        {
            throw new ForbiddenAccessException("Bạn không phải thuyền trưởng của tàu chạy chuyến này.");
        }

        var now = _dateTime.Now;
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
        _context.Incidents.Add(incident);
        // Admin quyết định hành động cho chuyến này sau (OperationDecision để trống)
        _context.IncidentTrips.Add(new IncidentTrip { IncidentId = incident.Id, TripId = trip.Id });
        await _context.SaveChangesAsync(cancellationToken);

        await _publisher.PublishIncidentRaisedAsync(
            new IncidentRaisedMessage(incident.Id, incident.IncidentType, incident.Severity, trip.Id, trip.BoatId, note, now),
            cancellationToken);

        return new SosReportDto(incident.Id, now);
    }
}
