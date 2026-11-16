using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.StaffAccounts;
using WaterbusSystem.Application.DTOs.Responses.StaffAccounts;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>Quản lý tài khoản Staff (soát vé) cho Admin. Không có thao tác xoá: ngừng dùng bằng cách tắt đăng nhập.</summary>
public interface IStaffAccountService
{
    Task<Guid> CreateAsync(CreateStaffAccountRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateStaffAccountRequest request, CancellationToken ct = default);
    Task ChangeStatusAsync(Guid id, ChangeStaffAccountStatusRequest request, CancellationToken ct = default);
    Task ResendInvitationAsync(Guid id, string reason, CancellationToken ct = default);
    Task<StaffAccountResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<PaginatedList<StaffAccountResponse>> ListAsync(GetStaffAccountsRequest request, CancellationToken ct = default);
}
