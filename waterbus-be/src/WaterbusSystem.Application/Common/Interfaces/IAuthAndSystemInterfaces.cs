namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng cấp phát JWT Token phục vụ xác thực người dùng
/// </summary>
public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string email, string fullName, IList<string> roles);
}

/// <summary>
/// Hợp đồng trích xuất thông tin người dùng hiện tại từ HttpContext
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserRole { get; }
}

/// <summary>
/// Hợp đồng lấy thời gian hệ thống chuẩn UTC
/// </summary>
public interface IDateTimeService
{
    DateTimeOffset Now { get; }
}
