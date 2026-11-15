namespace WaterbusSystem.Application.Features.Auth;

/// <summary>
/// Cổng đăng nhập. Mỗi cổng có danh sách role được phép (cấu hình tại <see cref="AuthOptions"/>).
/// Thêm cổng mới: thêm giá trị ở đây, mặc định ở <see cref="AuthOptions.RolesFor"/> và một endpoint mỏng ở AuthController.
/// </summary>
public enum AuthPortal
{
    /// <summary>Web vận hành: Admin, Captain.</summary>
    Web = 0,
    /// <summary>Ứng dụng Staff Scanner trên mobile: Staff.</summary>
    Scanner = 1
}
