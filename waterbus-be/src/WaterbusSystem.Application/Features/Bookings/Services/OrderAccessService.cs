using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Bookings.Services;

/// <summary>
/// Mô hình truy cập (BR-ID-01..07): Guest dùng Manage Order token; Passenger đăng nhập sở hữu Order sau khi liên kết
/// (chứng minh bằng token, KHÔNG dựa trên email trùng). Mọi thay đổi token/chủ sở hữu chạy trong
/// <see cref="ICommercialTransactionRunner"/> nên liên kết, đổi token, thu hồi được tuần tự hóa bằng khóa SQL:
/// token cũ bị replay sau khi đổi/thu hồi luôn bị từ chối, hai tài khoản cùng liên kết chỉ một bên thắng.
/// </summary>
public sealed class OrderAccessService(
    ICheckoutAccessService access,
    IAccessGrantRepository grants,
    IOrderRepository orders,
    IAccountAccessRepository accountReads,
    ICommercialTransactionRunner transactions,
    IUnitOfWork unitOfWork,
    ILogger<OrderAccessService> logger) : IOrderAccessService
{
    public Task<ClaimOrderResponse> ClaimAsync(Guid orderId, Guid accountId, string? token, CancellationToken ct = default) =>
        transactions.RunAsync(async () =>
        {
            await access.RequireTokenAsync(orderId, token, ct);
            var order = await orders.GetRequiredByIdAsync(orderId, ct);
            if (order.PassengerAccountId == accountId) return new ClaimOrderResponse(orderId, true);
            if (order.PassengerAccountId is not null)
                throw new ConcurrencyException("Đơn hàng đã được liên kết với tài khoản khác.");
            order.PassengerAccountId = accountId;
            await unitOfWork.SaveChangesAsync(ct);
            logger.LogInformation("Order {OrderId} linked to account {AccountId}", orderId, accountId);
            return new ClaimOrderResponse(orderId, false);
        }, ct);

    public Task<AccessTokenResponse> RotateAsync(Guid orderId, string? token, CancellationToken ct = default) =>
        transactions.RunAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            if (await access.IsOwnerAsync(orderId, ct))
            {
                // Chủ sở hữu đăng nhập: thu hồi mọi Manage Order link đang sống (vd link bị lộ) rồi cấp link mới.
                foreach (var g in await grants.GetActiveForOrderTrackedAsync(orderId, now, ct))
                    if (g.AccessType == CheckoutAccessService.ManageOrder) g.RevokedAt = now;
            }
            else
            {
                await access.RequireTokenAsync(orderId, token, ct);
                var current = await grants.GetActiveTrackedAsync(orderId, CheckoutAccessService.Hash(token!),
                    CheckoutAccessService.ManageOrder, now, ct) ?? throw new ForbiddenAccessException("Không có quyền truy cập đơn hàng này.");
                current.RevokedAt = now;
            }
            var (newToken, expiresAt) = access.IssueGrant(orderId, null, null, CheckoutAccessService.ManageOrder, now);
            await unitOfWork.SaveChangesAsync(ct);
            return new AccessTokenResponse(newToken, expiresAt);
        }, ct);

    public Task<RevokeAccessResponse> RevokeAsync(Guid orderId, string? token, CancellationToken ct = default) =>
        transactions.RunAsync(async () =>
        {
            await access.RequireAsync(orderId, token, ct);
            var now = DateTimeOffset.UtcNow;
            var active = await grants.GetActiveForOrderTrackedAsync(orderId, now, ct);
            foreach (var g in active) g.RevokedAt = now;
            await unitOfWork.SaveChangesAsync(ct);
            return new RevokeAccessResponse(active.Count);
        }, ct);

    public Task<AccessTokenResponse> IssueTripAccessAsync(Guid orderId, Guid bookingId, string? token, CancellationToken ct = default) =>
        transactions.RunAsync(async () =>
        {
            await access.RequireAsync(orderId, token, ct);
            var status = await accountReads.GetBookingStatusAsync(orderId, bookingId, ct)
                ?? throw new NotFoundException(nameof(Booking), bookingId);
            if (status is not (BookingStatus.Confirmed or BookingStatus.Completed))
                throw new ConcurrencyException("TripAccess chỉ cấp cho Booking đã thanh toán.");
            var (newToken, expiresAt) = access.IssueGrant(orderId, null, bookingId, CheckoutAccessService.PassengerTrip, DateTimeOffset.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            return new AccessTokenResponse(newToken, expiresAt);
        }, ct);

    public async Task<TripAccessDto> GetTripAccessAsync(string? tripAccessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tripAccessToken) || tripAccessToken.Length > 128)
            throw new ForbiddenAccessException("Liên kết theo dõi chuyến không hợp lệ.");
        var bookingId = await grants.FindTripAccessBookingAsync(CheckoutAccessService.Hash(tripAccessToken), DateTimeOffset.UtcNow, ct)
            ?? throw new ForbiddenAccessException("Liên kết theo dõi chuyến không hợp lệ.");
        return await accountReads.FindTripAccessAsync(bookingId, ct) ?? throw new NotFoundException(nameof(Booking), bookingId);
    }

    public async Task<TicketDetailDto> GetTicketAsync(Guid orderId, Guid ticketId, string? token, CancellationToken ct = default)
    {
        await access.RequireAsync(orderId, token, ct);
        return await accountReads.FindTicketAsync(orderId, ticketId, ct) ?? throw new NotFoundException(nameof(Ticket), ticketId);
    }

    public Task<PaginatedList<MyOrderSummaryDto>> GetMyOrdersAsync(Guid accountId, GetMyOrdersRequest request, CancellationToken ct = default) =>
        accountReads.GetOrdersPageAsync(accountId, request, ct);

    public async Task<TicketDetailDto> GetMyTicketAsync(Guid accountId, Guid ticketId, CancellationToken ct = default) =>
        await accountReads.FindTicketForAccountAsync(accountId, ticketId, ct) ?? throw new NotFoundException(nameof(Ticket), ticketId);
}
