namespace WaterbusSystem.Application.Features.Trips;

/// <summary>Quy tắc "khi nào thuyền trưởng được bấm nút nào" (section CaptainOperations). Admin không bị các ràng buộc này.</summary>
public sealed class CaptainOperationOptions
{
    public const string SectionName = "CaptainOperations";

    /// <summary>
    /// Bật các ràng buộc theo giờ, theo luồng Mở đón khách → Bắt đầu và theo vị trí tàu. Tắt = chỉ theo vòng đời trạng thái
    /// (dùng khi cần thao tác tự do, ví dụ demo không có GPS).
    /// </summary>
    public bool Enforce { get; set; } = true;

    /// <summary>Được mở đón khách từ bao nhiêu phút trước giờ khởi hành dự kiến.</summary>
    public int OpenBoardingLeadMinutes { get; set; } = 30;

    /// <summary>Tàu phải cách bến cuối không quá bấy nhiêu mét mới được bấm "Đã cập bến".</summary>
    public int ArriveRadiusM { get; set; } = 150;

    /// <summary>Vị trí tàu cũ hơn ngưỡng này (giây) không được dùng để cho phép cập bến.</summary>
    public int PositionMaxAgeSeconds { get; set; } = 120;
}
