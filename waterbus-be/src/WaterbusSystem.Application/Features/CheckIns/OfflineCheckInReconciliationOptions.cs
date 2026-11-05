namespace WaterbusSystem.Application.Features.CheckIns;

/// <summary>
/// Giới hạn độ tin cậy của <c>occurredAtDevice</c> khi đồng bộ check-in offline (BR-OFF-04/05): giờ quét
/// do thiết bị tự khai không được coi là bằng chứng tuyệt đối. BR v2.0 không quy định số giờ cụ thể cho
/// "offline bao lâu là hợp lý" - <see cref="MaxOfflineSkewHours"/> là ngưỡng cấu hình được, chỉ giới hạn
/// phạm vi bất thường (quá xa so với lúc server nhận), KHÔNG giải quyết triệt để việc giả mạo đồng hồ thiết
/// bị trong phạm vi ngưỡng (cần cơ chế ký số/xác thực khác nếu muốn chống tuyệt đối).
/// </summary>
public class OfflineCheckInReconciliationOptions
{
    public const string SectionName = "CheckIn:OfflineReconciliation";

    /// <summary>
    /// occurredAtDevice lùi quá xa so với lúc server nhận (now) thì bị coi là không đủ tin cậy để tự động
    /// cho check-in thành công (outcome "InvalidDeviceTime"). Mặc định 6 giờ - đủ cho ca làm việc mất kết
    /// nối thông thường (BR-OFF-01: đồng bộ sát giờ check-in khi có kết nối), chỉnh lại nếu vận hành thực
    /// tế cần dài/ngắn hơn.
    /// </summary>
    public int MaxOfflineSkewHours { get; set; } = 6;
}
