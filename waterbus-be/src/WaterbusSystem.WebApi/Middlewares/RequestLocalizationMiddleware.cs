using System.Globalization;

namespace WaterbusSystem.WebApi.Middlewares;

/// <summary>
/// Middleware tự động phát hiện header Accept-Language (vi, en, ...) để thiết lập ngữ cảnh văn hóa/ngôn ngữ
/// </summary>
public class RequestLocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly string[] SupportedCultures = { "vi", "en" };

    public RequestLocalizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var acceptLanguage = context.Request.Headers["Accept-Language"].ToString();
        var cultureName = "vi"; // Mặc định là Tiếng Việt

        if (!string.IsNullOrWhiteSpace(acceptLanguage))
        {
            var primaryLang = acceptLanguage.Split(',')[0].Trim().ToLowerInvariant();
            if (primaryLang.StartsWith("en"))
            {
                cultureName = "en";
            }
        }

        var culture = new CultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        await _next(context);
    }
}
