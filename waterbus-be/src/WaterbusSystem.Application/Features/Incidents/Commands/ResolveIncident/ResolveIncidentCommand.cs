using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Incidents.Commands.ResolveIncident;

public record ResolvedIncidentDto(Guid IncidentId, string Status, DateTimeOffset ResolvedAt);

/// <summary>
/// Admin đóng một sự cố đang mở (vd cảnh báo thời tiết đã hết nguy hiểm).
/// Chỉ đổi trạng thái Incident; KHÔNG đổi Trip.Status và không đụng tới quyết định đã ghi ở IncidentTrip (BR-INC-03).
/// </summary>
public record ResolveIncidentCommand(Guid IncidentId) : IRequest<ResolvedIncidentDto>;

public class ResolveIncidentCommandValidator : AbstractValidator<ResolveIncidentCommand>
{
    public ResolveIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty().WithMessage("Thiếu mã sự cố.");
    }
}

public class ResolveIncidentCommandHandler : IRequestHandler<ResolveIncidentCommand, ResolvedIncidentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public ResolveIncidentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IDateTimeService dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ResolvedIncidentDto> Handle(ResolveIncidentCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var adminId)
            || !string.Equals(_currentUser.UserRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ Admin mới được đóng sự cố.");
        }

        var incident = await _context.Incidents
            .FirstOrDefaultAsync(i => i.Id == request.IncidentId && !i.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), request.IncidentId);

        if (incident.Status != IncidentConstants.Statuses.Open)
        {
            throw new ConflictException($"Sự cố đã ở trạng thái {incident.Status}, không thể đóng lại.");
        }

        var now = _dateTime.Now;
        incident.Status = IncidentConstants.Statuses.Resolved;
        incident.ResolvedAt = now;
        incident.HandledByAdminId = adminId;
        incident.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new ResolvedIncidentDto(incident.Id, incident.Status, now);
    }
}
