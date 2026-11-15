using Microsoft.Extensions.Options;

namespace WaterbusSystem.Application.Features.Tickets;

/// <summary>
/// Cấu hình tra cứu vé bằng mã đặt chỗ + email + OTP (section "TicketLookup").
/// Mặc định đã chặt; chỉ nên chỉnh khi có lý do vận hành.
/// </summary>
public sealed class TicketLookupOptions
{
    public const string SectionName = "TicketLookup";

    /// <summary>Thời gian sống của OTP.</summary>
    public int OtpTtlMinutes { get; set; } = 5;
    /// <summary>Số lần nhập sai tối đa cho một OTP; vượt quá thì OTP bị huỷ và phải xin mã mới.</summary>
    public int MaxVerifyAttempts { get; set; } = 5;
    /// <summary>Khoảng cách tối thiểu giữa hai lần gửi OTP cho cùng một cặp email + mã đặt chỗ.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;
    /// <summary>Số OTP tối đa được gửi trong 1 giờ cho một cặp email + mã đặt chỗ (chống spam hộp thư người mua).</summary>
    public int MaxSendsPerHour { get; set; } = 5;
}

/// <summary>Giá trị ngoài khoảng an toàn làm ứng dụng không khởi động.</summary>
public sealed class TicketLookupOptionsValidator : IValidateOptions<TicketLookupOptions>
{
    public ValidateOptionsResult Validate(string? name, TicketLookupOptions o)
    {
        var errors = new List<string>();
        if (o.OtpTtlMinutes is < 1 or > 15) errors.Add("TicketLookup:OtpTtlMinutes phải từ 1 đến 15.");
        if (o.MaxVerifyAttempts is < 1 or > 10) errors.Add("TicketLookup:MaxVerifyAttempts phải từ 1 đến 10.");
        if (o.ResendCooldownSeconds is < 10 or > 600) errors.Add("TicketLookup:ResendCooldownSeconds phải từ 10 đến 600.");
        if (o.MaxSendsPerHour is < 1 or > 30) errors.Add("TicketLookup:MaxSendsPerHour phải từ 1 đến 30.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
