using WaterbusSystem.Application.DTOs.Requests.Auth;
using WaterbusSystem.Application.DTOs.Responses.Auth;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Hợp đồng nghiệp vụ xác thực người dùng. Implementation thật nằm ở Infrastructure
/// vì cần ASP.NET Core Identity (UserManager/SignInManager) — hạ tầng, không thuộc Application.
/// </summary>
public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
