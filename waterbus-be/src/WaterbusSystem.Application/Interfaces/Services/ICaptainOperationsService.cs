using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.DTOs.Responses.Captains;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICaptainOperationsService
{
    Task EnsureSellableAsync(Guid boatId, CancellationToken ct = default);
    Task EnsureStartAsync(Trip trip, CancellationToken ct = default);
    Task ChangeAssignmentAsync(Boat boat, Guid? captainId, string reason, CancellationToken ct = default);
    Task AssignAsync(Guid boatId, AssignCaptainRequest request, CancellationToken ct = default);
    Task<CaptainAttendanceResponse> RecordAttendanceAsync(Guid tripId, RecordCaptainAttendanceRequest request, CancellationToken ct = default);
    Task<CaptainTripAttendanceResponse> GetAttendanceAsync(Guid tripId, CancellationToken ct = default);
    Task<PaginatedList<CaptainAuditEvent>> AuditAsync(GetCaptainEventsRequest request, CancellationToken ct = default);
    Task<PaginatedList<CaptainAlert>> AlertsAsync(GetCaptainEventsRequest request, CancellationToken ct = default);
    Task ResolveAlertAsync(Guid id, CancellationToken ct = default);
    Task<int> CheckAttendanceAsync(CancellationToken ct = default);
}
