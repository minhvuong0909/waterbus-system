namespace WaterbusSystem.Application.DTOs.Responses.Auth;

/// <summary>
/// Phản hồi sau khi đăng nhập thành công, kèm JWT Token
/// </summary>
public record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    IList<string> Roles,
    string Token);
