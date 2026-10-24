using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.Auth;
using WaterbusSystem.Application.DTOs.Responses.Auth;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Xác thực người dùng và cấp phát JWT Token
/// </summary>
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Đăng nhập tài khoản hệ thống (Admin, Captain, Staff, Passenger)
    /// </summary>
    [HttpPost("login")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request, HttpContext.RequestAborted);
        return Ok(response);
    }
}
