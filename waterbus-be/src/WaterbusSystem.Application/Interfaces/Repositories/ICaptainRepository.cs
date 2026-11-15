using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.DTOs.Responses.Captains;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface ICaptainRepository
{
    Task<CaptainResponse> GetAsync(Guid id, CancellationToken ct);
    Task<PaginatedList<CaptainResponse>> ListAsync(GetCaptainsRequest request, CancellationToken ct);
    Task<bool> IsEligibleAsync(Guid id, DateTimeOffset at, CancellationToken ct);
    Task<CaptainSessionState?> SessionAsync(Guid id, CancellationToken ct);
    Task<bool> LicenseExistsAsync(string license, Guid? exceptId, CancellationToken ct);
    Task<Boat> BoatAsync(Guid id, CancellationToken ct);
    Task<Boat?> AssignedBoatAsync(Guid captainId, CancellationToken ct);
    Task<List<Trip>> BoatTripsAsync(Guid boatId, CancellationToken ct);
    Task<Trip> TripAsync(Guid id, CancellationToken ct);
    Task<List<Trip>> PendingTripsAsync(CancellationToken ct);
    Task<CaptainAttendance?> AttendanceAsync(Guid tripId, Guid boatId, Guid version, CancellationToken ct);
    Task<bool> HasAlertAsync(string key, CancellationToken ct);
    Task<CaptainAlert> AlertAsync(Guid id, CancellationToken ct);
    Task<PaginatedList<CaptainAuditEvent>> AuditAsync(GetCaptainEventsRequest request, CancellationToken ct);
    Task<PaginatedList<CaptainAlert>> AlertsAsync(GetCaptainEventsRequest request, CancellationToken ct);
    void Add(CaptainAuditEvent entry);
    void Add(CaptainAlert alert);
    void Add(CaptainAttendance attendance);
}
