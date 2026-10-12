using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WaterbusSystem.WebApi.Middlewares;

namespace WaterbusSystem.WebApi.Filters;

/// <summary>
/// Attribute kiểm tra bắt buộc phải có Guest Token hợp lệ từ header X-Guest-Token
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireGuestTokenAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Items.ContainsKey(GuestAccessMiddleware.GuestOrderIdItemKey))
        {
            context.Result = new JsonResult(new
            {
                success = false,
                message = "Header 'X-Guest-Token' không hợp lệ hoặc đã hết hạn truy cập.",
                statusCode = StatusCodes.Status401Unauthorized
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }
    }
}
