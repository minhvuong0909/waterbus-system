using System.Text.Json.Serialization;

namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Thao tác vận hành làm đổi (hoặc ghi nhận quyết định về) trạng thái chuyến.
/// </summary>
// Nhận/ghi tên thao tác dạng chuỗi ("Suspend") trong JSON, vẫn chấp nhận số
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TripOperation
{
    /// <summary>Mở đón khách tại bến đầu: Scheduled -> Boarding.</summary>
    OpenBoarding = 1,
    /// <summary>Rời bến: Scheduled/Boarding -> EnRoute.</summary>
    Start = 2,
    /// <summary>Cập bến cuối: EnRoute -> Arrived.</summary>
    Arrive = 3,
    /// <summary>Đóng nghiệp vụ chuyến: Arrived -> Completed.</summary>
    Complete = 4,
    /// <summary>Tạm ngưng (có thể tiếp tục), không tự gây hoàn tiền: -> Suspended.</summary>
    Suspend = 5,
    /// <summary>Tiếp tục sau khi tạm ngưng: Suspended -> trạng thái trước đó.</summary>
    Resume = 6,
    /// <summary>Huỷ chuyến chưa bắt đầu phục vụ: -> Cancelled.</summary>
    Cancel = 7,
    /// <summary>Kết thúc sớm chuyến đã bắt đầu: -> Terminated.</summary>
    Terminate = 8,
    /// <summary>Admin xem xét sự cố và quyết định không tác động lên chuyến; không đổi trạng thái.</summary>
    NoAction = 9
}
