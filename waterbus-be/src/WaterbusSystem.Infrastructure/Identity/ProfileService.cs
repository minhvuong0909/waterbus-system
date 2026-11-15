using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>Hồ sơ của chính tài khoản đang đăng nhập (id lấy từ JWT, không nhận id từ client). Email không đổi qua đây.</summary>
public sealed class ProfileService(UserManager<ApplicationUser> users) : IProfileService
{
    public async Task<ProfileResponse> GetAsync(Guid accountId, CancellationToken ct = default) =>
        ToResponse(await FindAsync(accountId));

    public async Task<ProfileResponse> UpdateAsync(Guid accountId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(accountId);
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure("profile", e.Description)));
        return ToResponse(user);
    }

    private async Task<ApplicationUser> FindAsync(Guid accountId)
    {
        var user = await users.FindByIdAsync(accountId.ToString());
        return user is { IsActive: true } ? user : throw new NotFoundException(nameof(ApplicationUser), accountId);
    }

    private static ProfileResponse ToResponse(ApplicationUser u) =>
        new(u.Id, u.Email ?? string.Empty, u.FullName, u.PhoneNumber, u.EmailConfirmed);
}
