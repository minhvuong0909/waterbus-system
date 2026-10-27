using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class OrderRepository(ApplicationDbContext context) : IOrderRepository
{
    public async Task<PurchaseOrder> GetRequiredByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await context.PurchaseOrders
            .FirstOrDefaultAsync(
                order => order.Id == orderId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), orderId);
    }

    public void Add(PurchaseOrder order)
    {
        context.PurchaseOrders.Add(order);
    }


    public async Task<PurchaseOrder> GetForSubmissionAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await context.PurchaseOrders
            .Include(o => o.Bookings)
            .ThenInclude(b => b.SeatReservations)
            .FirstOrDefaultAsync(
                o => o.Id == orderId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(PurchaseOrder), orderId);
    }

    public Task<bool> HasFinancialTransactionsAsync(
    Guid orderId,
    CancellationToken cancellationToken = default)
    {
        return context.FinancialTransactions.AnyAsync(
            x => x.OrderId == orderId,
            cancellationToken);
    }

    public async Task<PurchaseOrder> GetDetailAsync(Guid orderId, CancellationToken ct) =>
        await context.PurchaseOrders.AsNoTracking().Include(o => o.Bookings).ThenInclude(b => b.Tickets).ThenInclude(t => t.SeatReservation)
            .Include(o => o.FinancialTransactions).FirstOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new NotFoundException(nameof(PurchaseOrder), orderId);

    public Task<List<PurchaseOrder>> GetExpiredAsync(DateTimeOffset now, DateTimeOffset legacyDeadline, CancellationToken ct) =>
        context.PurchaseOrders.Include(o => o.Bookings).ThenInclude(b => b.SeatReservations)
            .Where(o => (o.Status == WaterbusSystem.Domain.Enums.OrderStatus.PendingPayment || o.Status == WaterbusSystem.Domain.Enums.OrderStatus.Draft) &&
                (o.ExpiresAt <= now || (o.ExpiresAt == null && o.CreatedAt < legacyDeadline))).ToListAsync(ct);

}
