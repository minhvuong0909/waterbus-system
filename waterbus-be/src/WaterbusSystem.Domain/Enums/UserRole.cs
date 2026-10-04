namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Các vai trò người dùng trong hệ thống (RBAC)
/// </summary>
public enum UserRole
{
    Admin = 1,        // Toàn quyền quản trị danh mục bến bãi, giá vé, người dùng
    Captain = 2,      // Thuyền trưởng điều khiển tàu, gửi GPS, điều khiển audio
    Staff = 3,        // Nhân viên soát vé tại bến cảng (sử dụng máy quét QR offline/online)
    Passenger = 4     // Khách hàng đặt vé
}
