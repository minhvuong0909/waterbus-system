using System.Diagnostics;
using System.Text;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>
/// Đăng ký tài khoản khách hàng (Passenger) và xác minh email. Nằm ở Infrastructure vì dùng trực tiếp UserManager.
/// Bảo mật: role gán cứng; mật khẩu kiểm tra TRƯỚC khi xét email tồn tại (không dò email qua lỗi mật khẩu yếu);
/// email đã tồn tại băm mật khẩu giả để thời gian gần với nhánh tạo mới; phản hồi luôn như nhau;
/// email xác minh gửi nền (không chặn) và có chặn gửi lặp theo tài khoản; không log email hay token.
/// </summary>
public sealed class AccountService(
    UserManager<ApplicationUser> users,
    IVerificationEmailQueue mailQueue,
    IEmailCooldownStore cooldown,
    IOptions<EmailOptions> emailOptions,
    ILogger<AccountService> logger,
    TimeSpan? minResponseTime = null) : IAccountService
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
        "Nếu thông tin hợp lệ, chúng tôi đã gửi email xác minh. Vui lòng kiểm tra hộp thư (kể cả thư rác).");

    public async Task<AccountRequestAcceptedResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await RegisterCoreAsync(request);
        await PadAsync(started, ct);
        return result;
    }

    private async Task<AccountRequestAcceptedResponse> RegisterCoreAsync(RegisterRequest request)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            EmailConfirmed = false,
            IsActive = true
        };

        // 1. Mật khẩu phải hợp lệ với MỌI email, kể cả email đã có tài khoản.
        await EnsurePasswordIsValidAsync(user, request.Password);

        // 2. Email đã tồn tại: không tạo, không gửi, trả cùng phản hồi (băm giả để cân bằng thời gian).
        if (await users.FindByEmailAsync(email) is { } existing)
        {
            users.PasswordHasher.HashPassword(existing, request.Password);
            return Accepted;
        }

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            // Hai yêu cầu song song cùng email: bên thua coi như email đã tồn tại.
            if (created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")) return Accepted;
            throw new ValidationException(created.Errors.Select(e => new ValidationFailure("password", e.Description)));
        }

        // 3. Role gán cứng. Thiếu role là lỗi cấu hình: huỷ tài khoản vừa tạo để không để lại tài khoản không role.
        var role = await users.AddToRoleAsync(user, PassengerRole);
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            throw new InvalidOperationException("Không gán được role Passenger cho tài khoản mới (thiếu role?).");
        }

        await SendVerificationAsync(user);
        return Accepted;
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default)
    {
        var invalid = new ValidationException([new ValidationFailure("token", "Liên kết xác nhận không hợp lệ hoặc đã hết hạn.")]);
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || !user.IsActive) throw invalid;

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token)); }
        catch (FormatException) { throw invalid; }

        // Idempotent: liên kết còn hạn dùng lại vẫn thành công, email đã xác minh không bị thay đổi gì thêm.
        var result = await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) throw invalid;
    }

    public async Task<AccountRequestAcceptedResponse> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: false, IsActive: true }
            && await users.IsInRoleAsync(user, PassengerRole)
            && await cooldown.TryAcquireAsync($"account:verify:{user.Id:N}", ResendCooldown, ct))
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

    private async Task EnsurePasswordIsValidAsync(ApplicationUser user, string password)
    {
        var failures = new List<ValidationFailure>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            if (!result.Succeeded) failures.AddRange(result.Errors.Select(e => new ValidationFailure("password", e.Description)));
        }
        if (failures.Count > 0) throw new ValidationException(failures);
    }

    private async Task SendVerificationAsync(ApplicationUser user)
    {
        var baseUrl = emailOptions.Value.PublicBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogError("Email:PublicBaseUrl chưa cấu hình, không tạo được liên kết xác minh email.");
            return;
        }
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        mailQueue.Enqueue(user.Email!, user.FullName, $"{baseUrl}/verify-email?uid={user.Id:D}&token={encoded}");
    }
}
