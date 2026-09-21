using Microsoft.AspNetCore.Identity;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>
/// Thực thể Người dùng hệ thống kế thừa ASP.NET Core Identity
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? StaffCode { get; set; }
    public Guid? StationId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Thực thể Vai trò kế thừa ASP.NET Core Identity
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
