using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.Notifications;

namespace WaterbusSystem.Infrastructure.Services.Email;

public enum SmtpSecurity { StartTls, SslOnConnect, None }

/// <summary>Cấu hình SMTP (section "Email:Smtp"). Mật khẩu chỉ lấy từ user-secrets/biến môi trường.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    /// <summary>Cho phép Security=None (Mailpit/MailHog cục bộ). Bị từ chối ở môi trường Production.</summary>
    public bool AllowInsecureTransport { get; set; }
}

/// <summary>Kiểm tra cấu hình email: khi Enabled=true mà sai ⇒ host không khởi động (fail-fast, không fallback ngầm).</summary>
public static class EmailConfigurationRules
{
    public static IReadOnlyList<string> Validate(EmailOptions o, SmtpOptions s, bool isProduction)
    {
        var errors = new List<string>();
        if (!o.Enabled) return errors;
        if (!EmailAddressRules.IsValid(o.FromAddress)) errors.Add("Email:FromAddress phải là địa chỉ email hợp lệ.");
        if (!string.IsNullOrWhiteSpace(o.SupportEmail) && !EmailAddressRules.IsValid(o.SupportEmail.Trim()))
            errors.Add("Email:SupportEmail không hợp lệ.");
        if (string.IsNullOrWhiteSpace(s.Host)) errors.Add("Email:Smtp:Host là bắt buộc khi Email:Enabled=true.");
        if (s.Port is < 1 or > 65535) errors.Add("Email:Smtp:Port phải trong 1..65535.");
        if (s.TimeoutSeconds is < 1 or > 300) errors.Add("Email:Smtp:TimeoutSeconds phải trong 1..300.");
        if (s.Security == SmtpSecurity.None && (!s.AllowInsecureTransport || isProduction))
            errors.Add("Email:Smtp:Security=None chỉ được phép khi AllowInsecureTransport=true và KHÔNG ở Production.");
        if (string.IsNullOrWhiteSpace(s.Username) != string.IsNullOrWhiteSpace(s.Password))
            errors.Add("Email:Smtp:Username và Password phải cùng được cấu hình hoặc cùng bỏ trống.");
        if (!string.IsNullOrWhiteSpace(o.PublicBaseUrl))
        {
            if (!Uri.TryCreate(o.PublicBaseUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                errors.Add("Email:PublicBaseUrl phải là URL http(s) tuyệt đối.");
            else if (isProduction && uri.Scheme != "https")
                errors.Add("Email:PublicBaseUrl phải dùng https ở Production.");
        }
        else if (isProduction) errors.Add("Email:PublicBaseUrl là bắt buộc ở Production khi bật email.");
        if (o.MaxAttempts is < 1 or > 20) errors.Add("Email:MaxAttempts phải trong 1..20.");
        if (o.BatchSize is < 1 or > 200) errors.Add("Email:BatchSize phải trong 1..200.");
        if (o.MaxMessageAgeHours < 1) errors.Add("Email:MaxMessageAgeHours phải >= 1.");
        if (o.LeaseMinutes * 60 <= s.TimeoutSeconds * 2)
            errors.Add("Email:LeaseMinutes phải lớn hơn 2 lần Smtp:TimeoutSeconds (tránh claim lại khi đang gửi).");
        if (string.IsNullOrWhiteSpace(o.Cron)) errors.Add("Email:Cron là bắt buộc.");
        return errors;
    }
}

public sealed class EmailOptionsValidator(IOptions<SmtpOptions> smtp, IHostEnvironment environment) : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var errors = EmailConfigurationRules.Validate(options, smtp.Value, environment.IsProduction());
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
