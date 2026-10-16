namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Lý do cho quyết định trigger hoặc từ chối/bỏ qua trigger audio.
/// </summary>
public enum TriggerDecisionReason
{
    /// <summary>Đủ điều kiện và đã gửi lệnh trigger thành công tới Onboard Player.</summary>
    Triggered = 1,

    /// <summary>BoatId hoặc TripId trong vị trí không khớp với context chuyến tàu.</summary>
    BoatTripMismatch = 2,

    /// <summary>Chuyến đi không phải loại Sightseeing (chuyến Commuter không phát thuyết minh du lịch, BR-EXP-01).</summary>
    TripNotSightseeing = 3,

    /// <summary>Chuyến tàu không ở trạng thái EnRoute (Scheduled, Boarding, Suspended, Arrived, Completed, Cancelled, Terminated).</summary>
    TripNotEnRoute = 4,

    /// <summary>Đang có thông báo an toàn hoặc Captain override khẩn cấp (BR-EXP-06).</summary>
    SafetyAnnouncementActive = 5,

    /// <summary>POI đang ở trạng thái không hoạt động (IsActive = false).</summary>
    PoiInactive = 6,

    /// <summary>Liên kết RoutePoi đang ở trạng thái không hoạt động (IsActive = false).</summary>
    RoutePoiInactive = 7,

    /// <summary>POI chưa được thiết lập tọa độ (Latitude hoặc Longitude null).</summary>
    PoiCoordinatesMissing = 8,

    /// <summary>Bán kính TriggerRadiusM null hoặc <= 0; hệ thống không tự đặt bán kính mặc định.</summary>
    TriggerRadiusMissingOrInvalid = 9,

    /// <summary>Tàu đang ở ngoài bán kính kích hoạt của POI.</summary>
    OutsideTriggerRadius = 10,

    /// <summary>Không tìm thấy bản AudioGuide tiếng Việt ("vi") đã publish và có URI hợp lệ.</summary>
    NoPublishedVietnameseAudio = 11,

    /// <summary>POI này đã được kích hoạt trong lượt ghé hiện tại của chuyến tàu (chống phát lặp, BR-EXP-05).</summary>
    AlreadyTriggeredForVisit = 12,

    /// <summary>Gửi yêu cầu trigger tới Onboard Player thất bại.</summary>
    DispatchFailed = 13,

    /// <summary>Không có ứng viên POI nào thuộc Route của chuyến tàu.</summary>
    NoCandidatePois = 14,

    /// <summary>Vị trí Boat thiếu TripId/timestamp hoặc có tọa độ không hợp lệ.</summary>
    InvalidBoatPosition = 15,

    /// <summary>Ngữ cảnh Trip/Boat/Route thiếu ID hợp lệ.</summary>
    InvalidTripContext = 16,

    /// <summary>Tọa độ POI nằm ngoài phạm vi vĩ độ/kinh độ hợp lệ.</summary>
    PoiCoordinatesInvalid = 17,

    /// <summary>Nhiều vùng POI chồng nhau nhưng chưa biết thứ tự RoutePoi tiếp theo.</summary>
    AmbiguousRouteProgress = 18
}
