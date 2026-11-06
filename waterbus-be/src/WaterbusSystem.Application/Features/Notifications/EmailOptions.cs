namespace WaterbusSystem.Application.Features.Notifications;

/// <summary>Cấu hình thông báo email (section "Email"). Phần SMTP nằm ở Infrastructure ("Email:Smtp").</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Mặc định tắt: bản ghi outbox vẫn được tạo nhưng KHÔNG bao giờ bị coi là đã gửi.</summary>
    public bool Enabled { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Smart Waterbus";
    /// <summary>Gốc URL web công khai (vd https://waterbus.example) — dùng cho liên kết "Vé của tôi".</summary>
    public string? PublicBaseUrl { get; set; }
    /// <summary>Kênh hỗ trợ có thật; chỉ hiển thị khi được cấu hình (không bịa liên hệ).</summary>
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }

    public int MaxAttempts { get; set; } = 8;
    public int BatchSize { get; set; } = 20;
    /// <summary>Lease của worker; phải lớn hơn timeout gửi để tránh gửi trùng do claim lại sớm.</summary>
    public int LeaseMinutes { get; set; } = 10;
    /// <summary>Bản ghi cũ hơn mức này bị bỏ qua (Skipped) thay vì gửi thông báo lỗi thời.</summary>
    public int MaxMessageAgeHours { get; set; } = 48;
    public string Cron { get; set; } = "* * * * *";
}
