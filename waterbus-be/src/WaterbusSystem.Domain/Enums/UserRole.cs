namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Các vai trò người dùng trong hệ thống (RBAC)
/// </summary>
public enum UserRole
{
    Admin = 1,        // Toàn quyền quản trị danh mục bến bãi, giá vé, người dùng
    Dispatcher = 2,   // Điều phối lịch chạy, radar tàu, phê duyệt hoãn hủy do thời tiết
    Accountant = 3,   // Kế toán đối soát doanh thu cổng thanh toán VNPAY
    Captain = 4,      // Thuyền trưởng điều khiển tàu, gửi GPS, điều khiển audio
    Staff = 5,        // Nhân viên soát vé tại bến cảng (sử dụng máy quét QR offline/online)
    Passenger = 6     // Khách hàng đặt vé
}
