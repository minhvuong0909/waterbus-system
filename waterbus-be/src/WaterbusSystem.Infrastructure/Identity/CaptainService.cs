using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.DTOs.Responses.Captains;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Domain.Rules;

namespace WaterbusSystem.Infrastructure.Identity;

public sealed class CaptainService(UserManager<ApplicationUser> users, ICaptainRepository repository,
    IUnitOfWork unitOfWork, ICommercialTransactionRunner transactions, ICurrentUserService currentUser,
    IDateTimeService clock, ICaptainConnections connections, IPersonnelInvitationService invitations) : ICaptainService
{
    private Guid Admin()
    {
        if (currentUser.UserRole != "Admin" || !Guid.TryParse(currentUser.UserId, out var id))
            throw new ForbiddenAccessException("Chỉ Admin được quản lý Captain.");
        return id;
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new ValidationException(result.Errors.Select(e => new FluentValidation.Results.ValidationFailure(e.Code, e.Description)));
    }
    private static string Snapshot(ApplicationUser u) => JsonSerializer.Serialize(new { u.FullName, u.Email, u.PhoneNumber,
        u.CaptainLicenseNumber, u.CaptainLicenseExpiresOn, u.CaptainStatus, u.IsActive });
    private void Audit(ApplicationUser u, string action, string? old, string reason) => repository.Add(new CaptainAuditEvent {
        CaptainId = u.Id, ActedByAdminId = Admin(), OccurredAt = clock.Now, Action = action,
        OldValues = old, NewValues = Snapshot(u), Reason = reason });
    public async Task<Guid> CreateAsync(CreateCaptainRequest r, CancellationToken ct = default)
    {
        invitations.EnsureDeliveryConfigured();
        var id = await transactions.RunAsync(async () =>
    {
        Admin();
        var license = r.LicenseNumber.Trim().ToUpperInvariant();
        if (await repository.LicenseExistsAsync(license, null, ct)) throw new ConflictException("Số giấy phép đã tồn tại.");
        if (r.LicenseExpiresOn < CaptainRules.VietnamDate(clock.Now)) throw new ConflictException("Giấy phép phải còn hạn.");
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = r.Email.Trim(), Email = r.Email.Trim(),
            EmailConfirmed = false, FullName = r.FullName.Trim(), PhoneNumber = r.Phone.Trim(), IsActive = false,
            CaptainStatus = CaptainStatus.Inactive, PersonnelInvitationPending = true, CaptainLicenseNumber = license, CaptainLicenseExpiresOn = r.LicenseExpiresOn };
        Check(await users.CreateAsync(user));
        Check(await users.AddToRoleAsync(user, "Captain"));
        Audit(user, "Created", null, "Admin tạo tài khoản Captain");
        await unitOfWork.SaveChangesAsync(ct);
        return user.Id;
    }, ct);
        await invitations.SendAsync(id, "Admin created the personnel invitation", ct);
        return id;
    }
    private async Task<ApplicationUser> Required(Guid id, CancellationToken ct)
    {
        await repository.GetAsync(id, ct);
        return await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("Captain", id);
    }
    public async Task UpdateAsync(Guid id, UpdateCaptainRequest r, CancellationToken ct = default)
    {
        var emailChanged = false;
        await transactions.RunAsync(async () =>
        {
            Admin(); var user = await Required(id, ct); var old = Snapshot(user);
            var license = r.LicenseNumber.Trim().ToUpperInvariant();
            if (await repository.LicenseExistsAsync(license, id, ct)) throw new ConflictException("Số giấy phép đã tồn tại.");
            emailChanged = !string.Equals(user.Email, r.Email.Trim(), StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                if (user.CaptainStatus == CaptainStatus.Suspended)
                    throw new ConflictException("Move the Captain to Inactive before changing the activation email.");
                invitations.EnsureDeliveryConfigured();
                user.EmailConfirmed = false; user.IsActive = false; user.PersonnelInvitationPending = true;
                user.CaptainStatus = CaptainStatus.Inactive;
            }
            user.Email = r.Email.Trim(); user.UserName = user.Email;
            user.FullName = r.FullName.Trim();
            user.PhoneNumber = r.Phone.Trim(); user.CaptainLicenseNumber = license; user.CaptainLicenseExpiresOn = r.LicenseExpiresOn;
            user.CaptainSessionVersion = Guid.NewGuid();
            // Email changes invalidate the previous invitation in the profile transaction,
            // before the replacement invitation is sent in a separate transaction.
            Check(emailChanged ? await users.UpdateSecurityStampAsync(user) : await users.UpdateAsync(user));
            Audit(user, "ProfileUpdated", old, r.Reason.Trim());
            await unitOfWork.SaveChangesAsync(ct); return true;
        }, ct);
        connections.Revoke(id);
        if (emailChanged) await invitations.SendAsync(id, r.Reason, ct);
    }
    public async Task ChangeStatusAsync(Guid id, ChangeCaptainStatusRequest r, CancellationToken ct = default)
    {
        await transactions.RunAsync(async () =>
        {
            Admin(); var user = await Required(id, ct); var old = Snapshot(user);
            if (!Enum.IsDefined(r.Status)) throw new ConflictException("Trạng thái Captain không hợp lệ.");
            if (r.Status == CaptainStatus.Active && (user.PersonnelInvitationPending || user.PasswordHash is null || !user.EmailConfirmed)) throw new ConflictException("Captain must accept the invitation first.");
            if (r.Status != CaptainStatus.Active) user.PersonnelInvitationPending = false;
            user.CaptainStatus = r.Status; user.IsActive = r.Status == CaptainStatus.Active;
            user.CaptainSessionVersion = Guid.NewGuid();
            Check(await users.UpdateAsync(user)); Audit(user, "StatusChanged", old, r.Reason.Trim());
            await unitOfWork.SaveChangesAsync(ct); return true;
        }, ct);
        connections.Revoke(id);
    }
    public async Task ResendInvitationAsync(Guid id, string reason, CancellationToken ct = default)
    {
        Admin();
        await Required(id, ct);
        await invitations.SendAsync(id, reason, ct);
    }
    public Task<CaptainResponse> GetAsync(Guid id, CancellationToken ct = default) { Admin(); return repository.GetAsync(id, ct); }
    public Task<PaginatedList<CaptainResponse>> ListAsync(GetCaptainsRequest r, CancellationToken ct = default) { Admin(); return repository.ListAsync(r, ct); }
}
