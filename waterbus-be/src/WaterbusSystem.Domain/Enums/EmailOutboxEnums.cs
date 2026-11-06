namespace WaterbusSystem.Domain.Enums;

/// <summary>Loại email giao dịch. Cancelled và Terminated là hai loại riêng (ngữ cảnh/quyền lợi khác nhau).</summary>
public enum EmailNotificationType { BookingConfirmed = 1, TripCancelled = 2, TripTerminated = 3, RefundSucceeded = 4 }

/// <summary>
/// Pending: chờ gửi · Processing: đã được worker claim (lease) · Sent: provider đã chấp nhận ·
/// Failed: lỗi tạm thời, sẽ retry ở NextAttemptAt · DeadLetter: dừng hẳn (cần người xử lý) ·
/// Skipped: không còn đúng/quá hạn nên cố ý không gửi.
/// </summary>
public enum EmailOutboxStatus { Pending, Processing, Sent, Failed, DeadLetter, Skipped }
