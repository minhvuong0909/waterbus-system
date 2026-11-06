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
    /// <summary>
    /// Chính sách theo loại (xem <see cref="NotificationPolicy"/>). Thông báo vận hành (huỷ/chấm dứt chuyến) chưa gửi được
    /// sau ngần này giờ chuyển DeadLetter "StaleNeedsReview" để người vận hành xem xét — KHÔNG bị bỏ qua âm thầm.
    /// </summary>
    public int MaxMessageAgeHours { get; set; } = 48;
    /// <summary>Thông báo tài chính (xác nhận vé, hoàn tiền): số lần thử riêng, dài hơn để chịu được sự cố SMTP kéo dài nhiều ngày.</summary>
    public int FinancialMaxAttempts { get; set; } = 16;
    /// <summary>Thông báo tài chính chưa gửi sau ngần này giờ ⇒ DeadLetter "StaleNeedsReview" (cần người xem, không tự huỷ).</summary>
    public int FinancialMaxAgeHours { get; set; } = 168;
    public string Cron { get; set; } = "* * * * *";
}
