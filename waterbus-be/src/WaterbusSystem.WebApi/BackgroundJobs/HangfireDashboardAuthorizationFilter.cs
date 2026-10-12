using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;

namespace WaterbusSystem.WebApi.BackgroundJobs;

/// <summary>
/// Bảo vệ dashboard /hangfire. Trình duyệt không gửi JWT khi điều hướng trang nên dùng HTTP Basic Auth
/// với tài khoản lấy từ cấu hình (Hangfire:Dashboard:Username/Password). Không cấu hình thì chỉ mở ở Development.
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string? _username;
    private readonly string? _password;
    private readonly bool _isDevelopment;

    public HangfireDashboardAuthorizationFilter(string? username, string? password, bool isDevelopment)
    {
        _username = username;
        _password = password;
        _isDevelopment = isDevelopment;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (IsAuthorized(httpContext.Request.Headers.Authorization.ToString(), _username, _password, _isDevelopment))
        {
            return true;
        }

        if (HasCredentials(_username, _password))
        {
            // Yêu cầu trình duyệt hiện hộp thoại đăng nhập Basic
            httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire\"";
        }
        return false;
    }

    public static bool IsAuthorized(string? authorizationHeader, string? username, string? password, bool isDevelopment)
    {
        if (!HasCredentials(username, password))
        {
            return isDevelopment;
        }

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var header)
            || !string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(header.Parameter))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
        }
        catch (FormatException)
        {
            return false;
        }

        var separator = decoded.IndexOf(':');
        if (separator < 0) return false;

        // So sánh thời gian cố định để tránh timing attack
        return FixedTimeEquals(decoded[..separator], username!)
               & FixedTimeEquals(decoded[(separator + 1)..], password!);
    }

    private static bool HasCredentials(string? username, string? password) =>
        !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);

    private static bool FixedTimeEquals(string actual, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(actual)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
