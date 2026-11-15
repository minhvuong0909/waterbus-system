using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Auth;
using WaterbusSystem.Application.DTOs.Responses.Auth;
using WaterbusSystem.Application.Features.Auth;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>
/// Xác thực người dùng qua ASP.NET Core Identity và cấp phát JWT Token.
/// Nằm ở Infrastructure (không phải Application) vì phụ thuộc trực tiếp UserManager/SignInManager.
/// </summary>
public class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "Email hoặc mật khẩu không chính xác.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly AuthOptions _options;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IOptions<AuthOptions> options)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _options = options.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, AuthPortal portal, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            throw new AuthenticationException(InvalidCredentialsMessage);
        }

        // M11: lockoutOnFailure true để Identity tích lũy số lần sai và tự khóa tài khoản
        // (Lockout.MaxFailedAccessAttempts/DefaultLockoutTimeSpan cấu hình ở DependencyInjection.cs).
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            throw new AuthenticationException("Tài khoản đã bị tạm khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau.");
        }
        if (!result.Succeeded)
        {
            throw new AuthenticationException(InvalidCredentialsMessage);
        }

        var roles = await _userManager.GetRolesAsync(user);

        // Chỉ role thuộc cổng này mới được đăng nhập. Kiểm tra SAU khi mật khẩu đúng và dùng cùng thông báo
        // với sai mật khẩu, nên không dò được email nào thuộc role nào.
        var allowed = _options.RolesFor(portal);
        if (!roles.Any(role => allowed.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            throw new AuthenticationException(InvalidCredentialsMessage);
        }

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Email ?? "", user.FullName, roles);

        return new LoginResponse(user.Id, user.Email ?? "", user.FullName, roles, token);
    }
}
