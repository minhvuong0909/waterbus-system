using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>Vòng đời secure link của Order + liên kết tài khoản + vé/TripAccess (BR-ID-01..07).</summary>
public interface IOrderAccessService
{
    /// <summary>Liên kết Order vào Passenger đang đăng nhập; bắt buộc có Manage Order token hợp lệ (BR-ID-04).</summary>
    Task<ClaimOrderResponse> ClaimAsync(Guid orderId, Guid accountId, string? token, CancellationToken ct = default);
    /// <summary>Đổi token: thu hồi token cũ (hoặc mọi token nếu chủ sở hữu đăng nhập) và cấp token mới.</summary>
    Task<AccessTokenResponse> RotateAsync(Guid orderId, string? token, CancellationToken ct = default);
    /// <summary>Thu hồi mọi Manage Order / TripAccess link của Order.</summary>
    Task<RevokeAccessResponse> RevokeAsync(Guid orderId, string? token, CancellationToken ct = default);
    Task<AccessTokenResponse> IssueTripAccessAsync(Guid orderId, Guid bookingId, string? token, CancellationToken ct = default);
    Task<TripAccessDto> GetTripAccessAsync(string? tripAccessToken, CancellationToken ct = default);
    Task<TicketDetailDto> GetTicketAsync(Guid orderId, Guid ticketId, string? token, CancellationToken ct = default);
    Task<PaginatedList<MyOrderSummaryDto>> GetMyOrdersAsync(Guid accountId, GetMyOrdersRequest request, CancellationToken ct = default);
    Task<TicketDetailDto> GetMyTicketAsync(Guid accountId, Guid ticketId, CancellationToken ct = default);
}
