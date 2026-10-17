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

/// <summary>
/// Tra họ tên người dùng theo Id (nhật ký vận hành hiển thị "ai làm"). Tách khỏi IApplicationDbContext
/// vì tài khoản nằm trong ASP.NET Identity ở tầng Infrastructure.
/// </summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}
