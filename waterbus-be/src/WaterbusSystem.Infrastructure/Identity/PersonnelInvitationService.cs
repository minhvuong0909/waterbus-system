using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Services.Email;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>Không lưu token hoặc mật khẩu gốc. Stamp + cờ pending làm token một lần, resend vô hiệu token trước.</summary>
public sealed class PersonnelInvitationService(UserManager<ApplicationUser> users, ICommercialTransactionRunner transactions,
    IUnitOfWork unitOfWork, IVerificationEmailQueue mail, IOptions<EmailOptions> email,
    ICaptainConnections connections, ICaptainRepository captainRepository, IDateTimeService clock,
    IOptions<PersonnelInvitationOptions> invitationOptions, ICurrentUserService currentUser) : IPersonnelInvitationService
{
    private const string Purpose = "PersonnelActivation";
    private static string Snapshot(ApplicationUser user) => System.Text.Json.JsonSerializer.Serialize(new {
        user.IsActive, user.PersonnelInvitationPending, user.CaptainSessionVersion, user.CaptainStatus, user.EmailConfirmed });
    public void EnsureDeliveryConfigured()
    {
        if (!email.Value.Enabled || !EmailConfigurationRules.IsSafePublicBaseUrl(email.Value.PublicBaseUrl, allowLoopbackHttp: true))
            throw new ConflictException("Cần bật email và cấu hình PublicBaseUrl trước khi mời nhân sự.");
    }
    public async Task SendAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        if (currentUser.UserRole != "Admin" || !Guid.TryParse(currentUser.UserId, out var adminId)) throw new ForbiddenAccessException("Only Admin can invite personnel.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new ConflictException("An invitation reason is required, maximum 1000 characters.");
        EnsureDeliveryConfigured();
        var delivery = await transactions.RunAsync(async () =>
        {
            var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("Personnel", userId);
            var roles = await users.GetRolesAsync(user);
            if (!roles.Any(r => r is "Captain" or "Staff") || roles.Contains("Admin")) throw new ForbiddenAccessException("Chỉ mời nhân sự Captain/Staff.");
            if (user.CaptainStatus == CaptainStatus.Suspended) throw new ConflictException("Suspended Captain cannot receive an activation invitation.");
            if (user.EmailConfirmed && !user.PersonnelInvitationPending) throw new ConflictException("Tài khoản đã kích hoạt.");
            var old = Snapshot(user);
            user.PersonnelInvitationPending = true;
            user.IsActive = false;
            if (roles.Contains("Captain")) user.CaptainStatus = CaptainStatus.Inactive;
            user.CaptainSessionVersion = Guid.NewGuid();
            Check(await users.UpdateSecurityStampAsync(user));
            if (roles.Contains("Captain"))
            {
                captainRepository.Add(new CaptainAuditEvent { CaptainId = user.Id, ActedByAdminId = adminId,
                    Action = "InvitationSent", OccurredAt = clock.Now, Reason = reason.Trim(), OldValues = old,
                    NewValues = Snapshot(user) });
                await unitOfWork.SaveChangesAsync(ct);
            }
            var token = await users.GenerateUserTokenAsync(user, PersonnelInvitationTokenProvider.ProviderName, Purpose);
            return (user.Email!, user.FullName, Link: $"{email.Value.PublicBaseUrl!.TrimEnd('/')}/verify-email#uid={user.Id:D}&token={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}");
        }, ct);
        connections.Revoke(userId);
        if (!mail.TryEnqueueInvitation(delivery.Item1, delivery.FullName, delivery.Link, invitationOptions.Value.TokenLifetimeHours))
            throw new ConflictException("Chưa xếp hàng được email kích hoạt. Tài khoản vẫn chưa hoạt động; Admin cần gửi lại lời mời.");
    }
    public Task AcceptAsync(ConfirmEmailRequest request, CancellationToken ct = default) => transactions.RunAsync(async () =>
    {
        var invalid = new ValidationException([new("token", "Lời mời không hợp lệ, đã dùng hoặc hết hạn.")]);
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is not { PersonnelInvitationPending: true, EmailConfirmed: false } || user.CaptainStatus == CaptainStatus.Suspended) throw invalid;
        var roles = await users.GetRolesAsync(user);
        if (!roles.Any(r => r is "Captain" or "Staff") || roles.Contains("Admin")) throw invalid;
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token)); }
        catch (FormatException) { throw invalid; }
        if (!await users.VerifyUserTokenAsync(user, PersonnelInvitationTokenProvider.ProviderName, Purpose, token)) throw invalid;
        var reset = await users.GeneratePasswordResetTokenAsync(user);
        var old = Snapshot(user);
        user.EmailConfirmed = true; user.PersonnelInvitationPending = false; user.IsActive = true;
        if (roles.Contains("Captain")) user.CaptainStatus = CaptainStatus.Active;
        user.CaptainSessionVersion = Guid.NewGuid();
        user.AccessFailedCount = 0; user.LockoutEnd = null;
        var result = await users.ResetPasswordAsync(user, reset, request.Password);
        if (!result.Succeeded)
        {
            unitOfWork.DiscardTrackedChanges();
            if (result.Errors.Any(e => e.Code is "ConcurrencyFailure" or "InvalidToken")) throw invalid;
            Check(result);
        }
        if (roles.Contains("Captain"))
        {
            captainRepository.Add(new CaptainAuditEvent { CaptainId = user.Id, ActedByCaptainId = user.Id,
                Action = "InvitationAccepted", OccurredAt = clock.Now, Reason = "Email owner activated the account",
                OldValues = old, NewValues = Snapshot(user) });
            await unitOfWork.SaveChangesAsync(ct);
        }
        return true;
    }, ct);
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new ValidationException(result.Errors.Select(e => new FluentValidation.Results.ValidationFailure(e.Code, e.Description)));
    }
}
