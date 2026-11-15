using WaterbusSystem.Application.DTOs.Requests.Auth;
using WaterbusSystem.Application.DTOs.Responses.Auth;
using WaterbusSystem.Application.Features.Auth;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Hợp đồng nghiệp vụ xác thực người dùng. Implementation thật nằm ở Infrastructure
/// vì cần ASP.NET Core Identity (UserManager/SignInManager) — hạ tầng, không thuộc Application.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Đăng nhập ở một cổng. Role không thuộc cổng đó bị từ chối cùng thông báo với sai mật khẩu (không lộ loại tài khoản).
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, AuthPortal portal, CancellationToken cancellationToken = default);
}
