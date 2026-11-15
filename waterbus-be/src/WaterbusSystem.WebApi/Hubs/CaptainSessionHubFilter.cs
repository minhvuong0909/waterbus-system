using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Infrastructure.Identity;

namespace WaterbusSystem.WebApi.Hubs;

public sealed class CaptainSessionHubFilter(ICaptainRepository repository, ICaptainConnections connections) : IHubFilter
{
    private async Task Validate(HubCallerContext context)
    {
        if (context.User is { } user && !await CaptainSessionValidation.IsValidAsync(user, repository, context.ConnectionAborted))
        { context.Abort(); throw new HubException("Captain session revoked."); }
    }
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var caller = context.Context;
        if (caller.User?.IsInRole("Captain") == true && Guid.TryParse(caller.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            connections.Register(id, caller.ConnectionId, caller.Abort);
        try { await Validate(caller); await next(context); }
        catch { connections.Unregister(caller.ConnectionId); throw; }
    }
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    { await Validate(context.Context); return await next(context); }
    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    { connections.Unregister(context.Context.ConnectionId); await next(context, exception); }
}
