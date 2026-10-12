using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>
/// WebSocket/SSE từ trình duyệt không gửi được header Authorization, nên SignalR client
/// truyền JWT qua query string <c>access_token</c>. Chỉ chấp nhận cách này cho đường dẫn /hubs
/// để token không bị đọc từ query ở các REST endpoint khác.
/// </summary>
public static class SignalRTokenReader
{
    public const string HubPathPrefix = "/hubs";
    private const string QueryKey = "access_token";

    public static Task OnMessageReceived(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query[QueryKey].ToString();

        if (!string.IsNullOrEmpty(accessToken)
            && context.HttpContext.Request.Path.StartsWithSegments(HubPathPrefix))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
}
