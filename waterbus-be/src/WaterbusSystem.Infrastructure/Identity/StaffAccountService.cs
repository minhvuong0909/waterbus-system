using Microsoft.AspNetCore.Identity;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.StaffAccounts;
using WaterbusSystem.Application.DTOs.Responses.StaffAccounts;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>
/// Admin quản lý tài khoản Staff. Dùng chung cơ chế mời kích hoạt với Captain
/// (<see cref="IPersonnelInvitationService"/>): Admin không nhập mật khẩu, tài khoản chỉ Active sau khi nhân viên đặt mật khẩu.
/// </summary>
public sealed class StaffAccountService(UserManager<ApplicationUser> users, IStaffAccountRepository repository,
    ICommercialTransactionRunner transactions, ICurrentUserService currentUser,
    IPersonnelInvitationService invitations) : IStaffAccountService
{
    private const string CreatedReason = "Admin created the personnel invitation";
    private const string EmailChangedReason = "Admin changed the Staff email";

    private void EnsureAdmin()
    {
        if (currentUser.UserRole != "Admin" || !Guid.TryParse(currentUser.UserId, out _))
            throw new ForbiddenAccessException("Chỉ Admin được quản lý tài khoản Staff.");
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new ValidationException(result.Errors.Select(e => new FluentValidation.Results.ValidationFailure(e.Code, e.Description)));
    }

    private static string? NormalizeCode(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private async Task<ApplicationUser> RequiredAsync(Guid id, CancellationToken ct)
    {
        await repository.GetAsync(id, ct); // đảm bảo đây là tài khoản Staff, không phải Admin/Captain/Passenger
        return await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("Staff", id);
    }

    public async Task<Guid> CreateAsync(CreateStaffAccountRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();
        invitations.EnsureDeliveryConfigured();
        var code = NormalizeCode(request.StaffCode);
        var id = await transactions.RunAsync(async () =>
        {
            if (code is not null && await repository.StaffCodeExistsAsync(code, null, ct))
                throw new ConflictException("Mã nhân viên đã tồn tại.");
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = request.Email.Trim(), Email = request.Email.Trim(), EmailConfirmed = false,
                FullName = request.FullName.Trim(), PhoneNumber = request.Phone.Trim(), StaffCode = code,
                IsActive = false, PersonnelInvitationPending = true,
            };
            Check(await users.CreateAsync(user));
            Check(await users.AddToRoleAsync(user, "Staff"));
            return user.Id;
        }, ct);
        await invitations.SendAsync(id, CreatedReason, ct);
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateStaffAccountRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();
        var emailChanged = false;
        await transactions.RunAsync(async () =>
        {
            var user = await RequiredAsync(id, ct);
            var code = NormalizeCode(request.StaffCode);
            if (code is not null && await repository.StaffCodeExistsAsync(code, id, ct))
                throw new ConflictException("Mã nhân viên đã tồn tại.");
            emailChanged = !string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                invitations.EnsureDeliveryConfigured();
                // Email mới phải được xác minh lại: ngừng đăng nhập cho tới khi nhân viên kích hoạt.
                user.EmailConfirmed = false; user.IsActive = false; user.PersonnelInvitationPending = true;
            }
            user.Email = request.Email.Trim(); user.UserName = user.Email;
            user.FullName = request.FullName.Trim(); user.PhoneNumber = request.Phone.Trim(); user.StaffCode = code;
            Check(emailChanged ? await users.UpdateSecurityStampAsync(user) : await users.UpdateAsync(user));
            return true;
        }, ct);
        if (emailChanged) await invitations.SendAsync(id, EmailChangedReason, ct);
    }

    public async Task ChangeStatusAsync(Guid id, ChangeStaffAccountStatusRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();
        await transactions.RunAsync(async () =>
        {
            var user = await RequiredAsync(id, ct);
            if (request.IsActive && (user.PersonnelInvitationPending || user.PasswordHash is null || !user.EmailConfirmed))
                throw new ConflictException("Nhân viên phải kích hoạt tài khoản qua email trước khi được bật đăng nhập.");
            user.IsActive = request.IsActive;
            Check(await users.UpdateAsync(user));
            return true;
        }, ct);
    }

    public async Task ResendInvitationAsync(Guid id, string reason, CancellationToken ct = default)
    {
        EnsureAdmin();
        await RequiredAsync(id, ct);
        await invitations.SendAsync(id, reason, ct);
    }

    public Task<StaffAccountResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAdmin();
        return repository.GetAsync(id, ct);
    }

    public Task<PaginatedList<StaffAccountResponse>> ListAsync(GetStaffAccountsRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();
        return repository.ListAsync(request, ct);
    }
}
