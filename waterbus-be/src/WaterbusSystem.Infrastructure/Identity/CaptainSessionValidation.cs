using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Infrastructure.Identity;

public static class CaptainSessionValidation
{
    public const string VersionClaim = "captainVersion";
    public static async Task<bool> IsValidAsync(ClaimsPrincipal principal, ICaptainRepository repository, CancellationToken ct)
    {
        if (!principal.IsInRole("Captain")) return true;
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !Guid.TryParse(principal.FindFirstValue(VersionClaim), out var version)) return false;
        var state = await repository.SessionAsync(id, ct);
        return state is { IsActive: true, IsCaptain: true, Status: CaptainStatus.Active } && state.Version == version;
    }
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        if (!await IsValidAsync(context.Principal!, context.HttpContext.RequestServices.GetRequiredService<ICaptainRepository>(), context.HttpContext.RequestAborted))
            context.Fail("Captain session revoked.");
    }
}
