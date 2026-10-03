using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Auth.Commands.Login;
using WaterbusSystem.Infrastructure.Identity;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Xác thực người dùng và cấp phát JWT Token
/// </summary>
public class AuthController : BaseApiController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    /// <summary>
    /// Đăng nhập tài khoản hệ thống (Admin, Captain, Staff, Passenger)
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không chính xác." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không chính xác." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Email ?? "", user.FullName, roles);

        return Ok(new AuthResponseDto(user.Id, user.Email ?? "", user.FullName, roles, token));
    }
}
