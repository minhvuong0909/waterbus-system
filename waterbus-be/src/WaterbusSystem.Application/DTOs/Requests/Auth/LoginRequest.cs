namespace WaterbusSystem.Application.DTOs.Requests.Auth;

/// <summary>
/// Yêu cầu đăng nhập hệ thống bằng email + mật khẩu
/// </summary>
public record LoginRequest(string Email, string Password);
