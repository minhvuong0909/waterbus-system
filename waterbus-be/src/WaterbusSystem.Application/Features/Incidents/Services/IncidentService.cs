using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Incidents;
using WaterbusSystem.Application.DTOs.Responses.Incidents;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Incidents.Services;

public sealed class IncidentService(
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTimeService dateTime) : IIncidentService
{
    public Task<IncidentDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)=>
        incidents.GetDetailAsync(id, cancellationToken);

    public Task<PaginatedList<IncidentSummaryResponse>> GetPageAsync(GetIncidentsRequest request, CancellationToken cancellationToken = default) =>
        incidents.GetPageAsync(request, cancellationToken);

    //Admin đóng một sự cố đang mở (vd cảnh báo thời tiết đã hết nguy hiểm).
    // Chỉ đổi trạng thái Incident; KHÔNG đổi Trip.Status và không đụng tới quyết định đã ghi ở IncidentTrip (BR-INC-03).
    public async Task<ResolveIncidentResponse> ResolveAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(currentUser.UserId, out var adminId)
            || !string.Equals(currentUser.UserRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ Admin mới được đóng sự cố.");
        }

        var incident = await incidents.GetRequiredByIdAsync(incidentId, cancellationToken);

        if(incident.Status != IncidentConstants.Statuses.Open)
        {
            throw new ConflictException($"Sự cố đã ở trạng thái {incident.Status}, không thể đóng lại.");
        }

        var now = dateTime.Now;
        incident.Status = IncidentConstants.Statuses.Resolved;
        incident.ResolvedAt = now;
        incident.HandledByAdminId = adminId;
        incident.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResolveIncidentResponse(incident.Id, incident.Status, now);
    }
}