using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
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
    private readonly IAccountService _accountService;

    public AuthController(IAuthService authService, IAccountService accountService)
    {
        _authService = authService;
        _accountService = accountService;
    }

    /// <summary>
    /// Đăng nhập tài khoản hệ thống (Admin, Captain, Staff, Passenger)
    /// </summary>
    [HttpPost("login")]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request, HttpContext.RequestAborted);
        return Ok(response);
    }

    /// <summary>
    /// Khách đăng ký tài khoản khách hàng (Passenger). Phản hồi luôn như nhau dù email đã có tài khoản hay chưa.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(AccountRequestAcceptedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountRequestAcceptedResponse>> Register([FromBody] RegisterRequest request) =>
        Ok(await _accountService.RegisterAsync(request, HttpContext.RequestAborted));

    /// <summary>Xác minh email bằng liên kết gửi về hộp thư và đặt mật khẩu để kích hoạt tài khoản.</summary>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        await _accountService.ConfirmEmailAsync(request, HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Gửi lại email xác minh. Phản hồi luôn như nhau; có chặn gửi lặp theo tài khoản.</summary>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(AccountRequestAcceptedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountRequestAcceptedResponse>> ResendVerification([FromBody] ResendVerificationRequest request) =>
        Ok(await _accountService.ResendVerificationAsync(request, HttpContext.RequestAborted));
}
