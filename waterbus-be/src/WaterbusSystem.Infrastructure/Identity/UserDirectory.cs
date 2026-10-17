using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>Tra họ tên từ bảng tài khoản ASP.NET Identity.</summary>
public class UserDirectory : IUserDirectory
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserDirectory(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await _userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }
}
