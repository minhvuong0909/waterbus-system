using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Accounts.Services;

/// <summary>
/// Gắn đơn Guest vào tài khoản theo email đã xác minh. Chạy trong <see cref="ICommercialTransactionRunner"/> như
/// <c>OrderAccessService.ClaimAsync</c> để tuần tự hoá với thao tác claim bằng token chạy song song; RowVersion của đơn
/// chặn ghi đè. Mỗi lần gắn tối đa <see cref="MaxPerCall"/> đơn để không giữ transaction dài; còn đơn thì gọi lại.
/// </summary>
public sealed class OrderLinkingService(
    IUserDirectory users,
    IOrderLinkingRepository repository,
    ICommercialTransactionRunner transactions,
    IUnitOfWork unitOfWork,
    ILogger<OrderLinkingService> logger) : IOrderLinkingService
{
    public const int MaxPerCall = 200;
    private const string NotVerified = "Email của tài khoản chưa được xác minh nên chưa thể gắn đơn.";

    public async Task<LinkableOrdersResponse> CountLinkableAsync(Guid accountId, CancellationToken ct = default)
    {
        var email = await users.GetVerifiedEmailAsync(accountId, ct) ?? throw new ForbiddenAccessException(NotVerified);
        return new LinkableOrdersResponse(await repository.CountLinkableAsync(email, ct));
    }

    public async Task<LinkOrdersResponse> LinkByVerifiedEmailAsync(Guid accountId, CancellationToken ct = default)
    {
        var email = await users.GetVerifiedEmailAsync(accountId, ct) ?? throw new ForbiddenAccessException(NotVerified);
        return await transactions.RunAsync(async () =>
        {
            var orders = await repository.GetLinkableTrackedAsync(email, MaxPerCall, ct);
            foreach (var order in orders) order.PassengerAccountId = accountId;
            if (orders.Count > 0) await unitOfWork.SaveChangesAsync(ct);

            // Không log email, chỉ số lượng và id tài khoản.
            logger.LogInformation("Linked {Count} orders to account {AccountId} by verified email", orders.Count, accountId);
            var remaining = orders.Count == MaxPerCall ? await repository.CountLinkableAsync(email, ct) : 0;
            return new LinkOrdersResponse(orders.Count, remaining);
        }, ct);
    }
}
