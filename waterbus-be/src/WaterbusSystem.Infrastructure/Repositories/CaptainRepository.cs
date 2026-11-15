using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.DTOs.Responses.Captains;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Domain.Rules;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class CaptainRepository(ApplicationDbContext db) : ICaptainRepository
{
    private IQueryable<ApplicationUser> Captains => db.Users.AsNoTracking().Where(u =>
        db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Captain")));

    private IQueryable<CaptainResponse> Responses(IQueryable<ApplicationUser> users) => users.Select(u => new CaptainResponse(u.Id, u.FullName,
        u.Email!, u.PhoneNumber, u.CaptainLicenseNumber, u.CaptainLicenseExpiresOn,
        u.CaptainStatus ?? CaptainStatus.Inactive,
        db.Boats.Where(b => b.CaptainUserId == u.Id).Select(b => (Guid?)b.Id).FirstOrDefault(),
        u.PersonnelInvitationPending, u.EmailConfirmed));

    public async Task<CaptainResponse> GetAsync(Guid id, CancellationToken ct) =>
        await Responses(Captains.Where(u => u.Id == id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Captain", id);
    public Task<PaginatedList<CaptainResponse>> ListAsync(GetCaptainsRequest request, CancellationToken ct)
    {
        var q = Captains;
        if (!string.IsNullOrWhiteSpace(request.Search)) q = q.Where(u => u.FullName.Contains(request.Search) || u.Email!.Contains(request.Search));
        if (request.Status.HasValue) q = q.Where(u => (u.CaptainStatus ?? CaptainStatus.Inactive) == request.Status);
        return Responses(q.OrderBy(u => u.FullName).ThenBy(u => u.Id)).ToPaginatedListAsync(request.Page, request.PageSize, ct);
    }
    public async Task<bool> IsEligibleAsync(Guid id, DateTimeOffset at, CancellationToken ct)
    {
        var state = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new {
            IsActive = u.IsActive && !u.PersonnelInvitationPending, u.CaptainStatus, u.CaptainLicenseNumber, u.CaptainLicenseExpiresOn,
            IsCaptain = db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Captain"))
        }).FirstOrDefaultAsync(ct);
        return state is not null && CaptainRules.IsEligible(state.IsActive, state.IsCaptain, state.CaptainStatus,
            state.CaptainLicenseNumber, state.CaptainLicenseExpiresOn, at);
    }
    // Biểu diễn SQL tương đương CaptainRules.IsEligible để lọc trước phân trang và xác nhận thanh toán.
    public static IQueryable<Guid> EligibleIds(ApplicationDbContext db, DateTimeOffset at)
    {
        var date = CaptainRules.VietnamDate(at);
        return db.Users.Where(u => u.IsActive && !u.PersonnelInvitationPending && u.CaptainStatus == CaptainStatus.Active &&
            u.CaptainLicenseNumber != null && u.CaptainLicenseNumber.Trim() != "" && u.CaptainLicenseExpiresOn >= date &&
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Captain")))
            .Select(u => u.Id);
    }
    public Task<CaptainSessionState?> SessionAsync(Guid id, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(u => u.Id == id).Select(u => new CaptainSessionState(u.IsActive && !u.PersonnelInvitationPending,
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Captain")),
            u.CaptainStatus, u.CaptainSessionVersion)).FirstOrDefaultAsync(ct);
    public Task<bool> LicenseExistsAsync(string license, Guid? exceptId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.CaptainLicenseNumber == license && u.Id != exceptId, ct);
    public async Task<Boat> BoatAsync(Guid id, CancellationToken ct) => await db.Boats.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException(nameof(Boat), id);
    public Task<Boat?> AssignedBoatAsync(Guid captainId, CancellationToken ct) => db.Boats.AsNoTracking().FirstOrDefaultAsync(b => b.CaptainUserId == captainId, ct);
    public Task<List<Trip>> BoatTripsAsync(Guid boatId, CancellationToken ct) => db.Trips.AsNoTracking().Where(t => t.BoatId == boatId).ToListAsync(ct);
    public async Task<Trip> TripAsync(Guid id, CancellationToken ct) => await db.Trips.AsNoTracking().Include(t => t.Boat).Include(t => t.TripStopCalls)
        .FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(Trip), id);
    public Task<int> CreateDueAttendanceAlertsAsync(DateTimeOffset at, int preparationMinutes, int batchSize, CancellationToken ct) =>
        CaptainAttendanceAlertWriter.InsertAsync(db, at, preparationMinutes, batchSize, ct);
    public Task<CaptainAttendance?> AttendanceAsync(Guid tripId, Guid boatId, Guid version, CancellationToken ct) => db.Set<CaptainAttendance>().AsNoTracking()
        .FirstOrDefaultAsync(a => a.TripId == tripId && a.BoatId == boatId && a.AssignmentVersion == version, ct);
    public async Task<CaptainAlert> AlertAsync(Guid id, CancellationToken ct) => await db.Set<CaptainAlert>().FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException(nameof(CaptainAlert), id);
    public Task<PaginatedList<CaptainAuditEvent>> AuditAsync(GetCaptainEventsRequest r, CancellationToken ct) => db.Set<CaptainAuditEvent>().AsNoTracking()
        .Where(a => (!r.CaptainId.HasValue || a.CaptainId == r.CaptainId) && (!r.TripId.HasValue || a.TripId == r.TripId))
        .OrderByDescending(a => a.OccurredAt).ThenBy(a => a.Id).ToPaginatedListAsync(r.Page, r.PageSize, ct);
    public Task<PaginatedList<CaptainAlert>> AlertsAsync(GetCaptainEventsRequest r, CancellationToken ct) => db.Set<CaptainAlert>().AsNoTracking()
        .Where(a => (!r.CaptainId.HasValue || a.CaptainId == r.CaptainId) && (!r.TripId.HasValue || a.TripId == r.TripId))
        .OrderByDescending(a => a.OccurredAt).ThenBy(a => a.Id).ToPaginatedListAsync(r.Page, r.PageSize, ct);
    public void Add(CaptainAuditEvent e) => db.Add(e);
    public void Add(CaptainAttendance e) => db.Add(e);
    public void Add(CaptainAlert e) => db.Add(e);
}
