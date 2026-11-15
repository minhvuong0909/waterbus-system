using System.Diagnostics;
using System.Text;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Infrastructure.Services.Email;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>
/// Đăng ký tài khoản khách hàng (Passenger) và xác minh email. Nằm ở Infrastructure vì dùng trực tiếp UserManager.
/// Tài khoản mới chưa có mật khẩu. Chỉ người giữ token gửi đến hộp thư mới có thể đặt mật khẩu và kích hoạt.
/// </summary>
public sealed class AccountService(
    UserManager<ApplicationUser> users,
    IUnitOfWork unitOfWork,
    IVerificationEmailQueue mailQueue,
    IEmailCooldownStore cooldown,
    IOptions<EmailOptions> emailOptions,
    ILogger<AccountService> logger,
    TimeSpan? minResponseTime = null, IPersonnelInvitationService? personnelInvitations = null) : IAccountService
{
    internal const string PassengerRole = "Passenger";
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Thời gian phản hồi tối thiểu của đăng ký / gửi lại. Nhánh "email mới" (ghi DB, tạo token) luôn chậm hơn nhánh
    /// "email đã có" vài chục mili giây; đệm cả hai về cùng một mức để không dò được email nào đã có tài khoản qua độ trễ.
    /// </summary>
    internal static readonly TimeSpan DefaultMinResponseTime = TimeSpan.FromMilliseconds(400);

    private readonly TimeSpan _minResponseTime = minResponseTime ?? DefaultMinResponseTime;

    private static readonly AccountRequestAcceptedResponse Accepted = new(
        "Nếu tài khoản có thể kích hoạt, hãy kiểm tra hộp thư (kể cả thư rác) hoặc yêu cầu gửi lại liên kết.");

    public async Task<AccountRequestAcceptedResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await RegisterCoreAsync(request, ct);
        await PadAsync(started, ct);
        return result;
    }

    private async Task<AccountRequestAcceptedResponse> RegisterCoreAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null) return Accepted;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            EmailConfirmed = false,
            IsActive = true
        };

        IdentityResult created;
        try
        {
            created = await users.CreateAsync(user);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Hai lượt có thể cùng vượt qua bước kiểm tra của Identity trước khi ghi vào unique index.
            unitOfWork.DiscardTrackedChanges();
            return Accepted;
        }
        if (!created.Succeeded)
        {
            // Hai yêu cầu song song cùng email: bên thua coi như email đã tồn tại.
            if (created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")) return Accepted;
            throw new ValidationException(created.Errors.Select(e => new ValidationFailure("email", e.Description)));
        }

        // Role gán cứng. Huỷ tài khoản vừa tạo nếu Identity không thể gán role.
        var role = await users.AddToRoleAsync(user, PassengerRole);
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            throw new InvalidOperationException("Không gán được role Passenger cho tài khoản mới (thiếu role?).");
        }

        // Đăng ký chiếm cùng khoá cooldown với gửi lại: bấm "Gửi lại" ngay sau khi đăng ký không sinh thêm email thứ hai.
        if (await cooldown.TryAcquireAsync(CooldownKey(user), ResendCooldown, ct))
            await SendVerificationAsync(user);
        return Accepted;
    }

    private static string CooldownKey(ApplicationUser user) => $"account:verify:{user.Id:N}";

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default)
    {
        if (personnelInvitations is not null && (await users.FindByIdAsync(request.UserId.ToString()))?.PersonnelInvitationPending == true)
        { await personnelInvitations.AcceptAsync(request, ct); return; }
        var invalid = new ValidationException([new ValidationFailure("token", "Liên kết xác nhận không hợp lệ hoặc đã hết hạn.")]);
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is not { IsActive: true, EmailConfirmed: false } || !await users.IsInRoleAsync(user, PassengerRole))
            throw invalid;

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token)); }
        catch (FormatException) { throw invalid; }

        if (!await users.VerifyUserTokenAsync(user, users.Options.Tokens.EmailConfirmationTokenProvider,
                UserManager<ApplicationUser>.ConfirmEmailTokenPurpose, token))
            throw invalid;

        // ResetPasswordAsync kiểm tra policy, thay cả hash cũ, đổi security stamp và lưu cùng EmailConfirmed.
        // EF Identity chỉ ghi một lần với ConcurrencyStamp; hai lượt kích hoạt không thể cùng thắng.
        var resetToken = await users.GeneratePasswordResetTokenAsync(user);
        user.EmailConfirmed = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        try
        {
            var result = await users.ResetPasswordAsync(user, resetToken, request.Password);
            if (result.Succeeded) return;

            if (result.Errors.Any(e => e.Code is "ConcurrencyFailure" or "InvalidToken")) throw invalid;
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure("password", e.Description)));
        }
        catch
        {
            unitOfWork.DiscardTrackedChanges();
            throw;
        }
    }

    public async Task<AccountRequestAcceptedResponse> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: false, IsActive: true }
            && await users.IsInRoleAsync(user, PassengerRole)
            && await cooldown.TryAcquireAsync(CooldownKey(user), ResendCooldown, ct))
        {
            await SendVerificationAsync(user);
        }
        await PadAsync(started, ct);
        return Accepted;
    }

    private async Task PadAsync(long startedTimestamp, CancellationToken ct)
    {
        var remaining = _minResponseTime - Stopwatch.GetElapsedTime(startedTimestamp);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
    }

    private async Task SendVerificationAsync(ApplicationUser user)
    {
        var baseUrl = emailOptions.Value.PublicBaseUrl?.Trim().TrimEnd('/');
        if (!EmailConfigurationRules.IsSafePublicBaseUrl(baseUrl, allowLoopbackHttp: true))
        {
            logger.LogError("Email:PublicBaseUrl không hợp lệ. Liên kết xác minh công khai yêu cầu HTTPS.");
            return;
        }
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        mailQueue.TryEnqueue(user.Email!, user.FullName, $"{baseUrl}/verify-email#uid={user.Id:D}&token={encoded}");
    }
}
