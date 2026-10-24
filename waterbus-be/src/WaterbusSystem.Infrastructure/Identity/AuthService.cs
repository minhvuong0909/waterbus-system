using Microsoft.AspNetCore.Identity;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Auth;
using WaterbusSystem.Application.DTOs.Responses.Auth;
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

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            throw new AuthenticationException(InvalidCredentialsMessage);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            throw new AuthenticationException(InvalidCredentialsMessage);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Email ?? "", user.FullName, roles);

        return new LoginResponse(user.Id, user.Email ?? "", user.FullName, roles, token);
    }
}
