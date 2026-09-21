using MediatR;

namespace WaterbusSystem.Application.Features.Auth.Commands.Login;

/// <summary>
/// DTO phản hồi sau khi đăng nhập thành công
/// </summary>
public record AuthResponseDto(
    Guid UserId,
    string Email,
    string FullName,
    IList<string> Roles,
    string Token);

/// <summary>
/// Command yêu cầu đăng nhập hệ thống
/// </summary>
public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;
