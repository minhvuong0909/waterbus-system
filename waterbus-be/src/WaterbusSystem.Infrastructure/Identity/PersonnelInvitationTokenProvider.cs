using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WaterbusSystem.Infrastructure.Identity;

public sealed class PersonnelInvitationOptions
{
    public double TokenLifetimeHours { get; set; } = 24;
}

public sealed class PersonnelInvitationTokenProvider : DataProtectorTokenProvider<ApplicationUser>
{
    public const string ProviderName = "PersonnelInvitation";
    public PersonnelInvitationTokenProvider(IDataProtectionProvider protection, IOptions<PersonnelInvitationOptions> options,
        ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
        : base(protection, Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions {
            Name = ProviderName, TokenLifespan = TimeSpan.FromHours(options.Value.TokenLifetimeHours) }), logger) { }
}
