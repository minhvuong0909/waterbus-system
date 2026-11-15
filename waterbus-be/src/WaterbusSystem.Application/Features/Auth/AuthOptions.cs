using Microsoft.Extensions.Options;

namespace WaterbusSystem.Application.Features.Auth;

/// <summary>
/// Cấu hình xác thực (section "Auth"). <c>Auth:Portals:{Web|Scanner}</c> là danh sách role được đăng nhập ở cổng đó.
/// Bỏ trống ⇒ dùng mặc định trong <see cref="RolesFor"/>. Mặc định nằm trong code (không đặt sẵn trong property)
/// vì binder cộng dồn mảng cấu hình vào mảng mặc định, gây trùng phần tử.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public static readonly string[] KnownRoles = ["Admin", "Captain", "Staff", "Passenger"];

    public Dictionary<string, string[]> Portals { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> RolesFor(AuthPortal portal)
    {
        if (Portals.TryGetValue(portal.ToString(), out var configured)) return configured;
        return portal switch
        {
            AuthPortal.Web => ["Admin", "Captain"],
            AuthPortal.Scanner => ["Staff"],
            _ => []
        };
    }
}

/// <summary>Cấu hình sai (cổng lạ, danh sách rỗng, role lạ) làm ứng dụng không khởi động, thay vì âm thầm khoá hoặc mở cổng.</summary>
public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var errors = new List<string>();
        foreach (var (portal, roles) in options.Portals)
        {
            if (!Enum.TryParse<AuthPortal>(portal, ignoreCase: true, out _))
            {
                errors.Add($"Auth:Portals:{portal} không phải cổng hợp lệ ({string.Join(", ", Enum.GetNames<AuthPortal>())}).");
                continue;
            }
            if (roles is null || roles.Length == 0)
            {
                errors.Add($"Auth:Portals:{portal} không được để trống danh sách role.");
                continue;
            }
            foreach (var role in roles.Where(r => !KnownRole(r)))
            {
                errors.Add($"Auth:Portals:{portal} chứa role không tồn tại: '{role}'.");
            }
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool KnownRole(string role) =>
        AuthOptions.KnownRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
