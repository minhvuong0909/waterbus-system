using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services;

/// <summary>
/// Trích xuất thông tin người dùng hiện tại từ HttpContext (claims của JWT token đã xác thực)
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNamesSub);

    public string? UserRole =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

    // JwtRegisteredClaimNames.Sub ("sub") - tránh thêm dependency System.IdentityModel.Tokens.Jwt chỉ vì 1 hằng số
    private const string JwtRegisteredClaimNamesSub = "sub";
}
