using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

/// <summary>
/// Đơn chưa gắn tài khoản theo email người mua. Điều kiện <c>PassengerAccountId IS NULL</c> được giữ nguyên dạng literal
/// để SQL Server dùng index lọc trên PurchaserEmail (so sánh email theo collation không phân biệt hoa thường của DB).
/// Đơn đang trong quá trình thanh toán (Draft, PendingPayment) không bị gắn, để không chen vào phiên mua của Guest.
/// </summary>
public sealed class OrderLinkingRepository(ApplicationDbContext context) : IOrderLinkingRepository
{
    private static readonly OrderStatus[] LinkableStatuses = [OrderStatus.Confirmed, OrderStatus.Cancelled, OrderStatus.Expired];

    public Task<int> CountLinkableAsync(string email, CancellationToken ct) => Query(email).AsNoTracking().CountAsync(ct);

    public Task<List<PurchaseOrder>> GetLinkableTrackedAsync(string email, int take, CancellationToken ct) =>
        Query(email).OrderBy(o => o.CreatedAt).Take(take).ToListAsync(ct);

    private IQueryable<PurchaseOrder> Query(string email) => context.PurchaseOrders
        .Where(o => !o.IsDeleted && o.PassengerAccountId == null && o.PurchaserEmail == email && LinkableStatuses.Contains(o.Status));
}
