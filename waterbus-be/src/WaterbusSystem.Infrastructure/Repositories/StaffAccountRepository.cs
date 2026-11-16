using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.StaffAccounts;
using WaterbusSystem.Application.DTOs.Responses.StaffAccounts;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class StaffAccountRepository(ApplicationDbContext db) : IStaffAccountRepository
{
    private IQueryable<ApplicationUser> Staff => db.Users.AsNoTracking().Where(u =>
        db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Staff")));

    private static IQueryable<StaffAccountResponse> Responses(IQueryable<ApplicationUser> users) =>
        users.Select(u => new StaffAccountResponse(u.Id, u.FullName, u.Email!, u.PhoneNumber, u.StaffCode,
            u.IsActive, u.PersonnelInvitationPending, u.EmailConfirmed));

    public async Task<StaffAccountResponse> GetAsync(Guid id, CancellationToken ct) =>
        await Responses(Staff.Where(u => u.Id == id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Staff", id);

    public Task<PaginatedList<StaffAccountResponse>> ListAsync(GetStaffAccountsRequest request, CancellationToken ct)
    {
        var query = Staff;
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(u => u.FullName.Contains(request.Search) || u.Email!.Contains(request.Search) ||
                                     (u.StaffCode != null && u.StaffCode.Contains(request.Search)));
        if (request.IsActive.HasValue) query = query.Where(u => u.IsActive == request.IsActive);
        return Responses(query.OrderBy(u => u.FullName).ThenBy(u => u.Id)).ToPaginatedListAsync(request.Page, request.PageSize, ct);
    }

    public Task<bool> StaffCodeExistsAsync(string staffCode, Guid? exceptId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.StaffCode == staffCode && u.Id != exceptId, ct);
}
