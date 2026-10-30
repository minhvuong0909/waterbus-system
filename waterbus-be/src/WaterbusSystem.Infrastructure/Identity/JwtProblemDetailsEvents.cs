using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>
/// M10: chuẩn hóa 401 (Challenge)/403 (Forbidden) của JWT về cùng hình dạng ProblemDetails + traceId
/// mà GlobalExceptionHandlingMiddleware đang dùng cho các lỗi khác, tránh FE phải xử lý 2 format lỗi.
/// Tách thành static method (giống SignalRTokenReader.OnMessageReceived) để unit test được trực tiếp
/// mà không cần dựng cả pipeline JWT.
/// </summary>
public static class JwtProblemDetailsEvents
{
    public static async Task OnChallenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        // LƯU Ý: WriteAsJsonAsync tự ghi đè ContentType về "application/json; charset=utf-8" nếu không
        // truyền contentType qua tham số - set Response.ContentType trước đó sẽ bị bỏ qua lặng lẽ.
        await context.Response.WriteAsJsonAsync(
            new
            {
                status = StatusCodes.Status401Unauthorized,
                title = "Xác thực thất bại.",
                detail = "Token không hợp lệ hoặc đã hết hạn.",
                instance = context.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier
            },
            options: null,
            contentType: "application/problem+json");
    }

    public static async Task OnForbidden(ForbiddenContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            new
            {
                status = StatusCodes.Status403Forbidden,
                title = "Không có quyền thực hiện thao tác này.",
                detail = "Tài khoản không đủ quyền truy cập tài nguyên này.",
                instance = context.Request.Path.Value,
                traceId = context.HttpContext.TraceIdentifier
            },
            options: null,
            contentType: "application/problem+json");
    }
}
