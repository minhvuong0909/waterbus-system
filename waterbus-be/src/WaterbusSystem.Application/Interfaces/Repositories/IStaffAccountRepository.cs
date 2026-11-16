using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.StaffAccounts;
using WaterbusSystem.Application.DTOs.Responses.StaffAccounts;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IStaffAccountRepository
{
    /// <summary>Ném NotFoundException khi không có tài khoản Staff với id này.</summary>
    Task<StaffAccountResponse> GetAsync(Guid id, CancellationToken ct);
    Task<PaginatedList<StaffAccountResponse>> ListAsync(GetStaffAccountsRequest request, CancellationToken ct);
    Task<bool> StaffCodeExistsAsync(string staffCode, Guid? exceptId, CancellationToken ct);
}
