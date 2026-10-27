using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class FinancialTransactionRepository(ApplicationDbContext context) : IFinancialTransactionRepository
{
    public void Add(FinancialTransaction transaction) => context.FinancialTransactions.Add(transaction);

    public Task<FinancialTransaction?> GetRequestedPaymentAsync(Guid orderId, CancellationToken ct) =>
        context.FinancialTransactions.FirstOrDefaultAsync(p => p.OrderId == orderId &&
            p.TransactionType == FinancialTransactionType.Payment && p.Status == FinancialTransactionStatus.Requested, ct);

    public Task<bool> HasPaymentAsync(Guid orderId, CancellationToken ct) =>
        context.FinancialTransactions.AnyAsync(p => p.OrderId == orderId && p.TransactionType == FinancialTransactionType.Payment, ct);

    public Task<FinancialTransaction?> GetVnPayPaymentAsync(string merchantReference, CancellationToken ct) =>
        context.FinancialTransactions.Include(p => p.Order).FirstOrDefaultAsync(p => p.MerchantReference == merchantReference &&
            p.GatewayName == "VNPAY" && p.TransactionType == FinancialTransactionType.Payment, ct);

    public Task<bool> HasIdempotencyKeyAsync(string key, CancellationToken ct) =>
        context.FinancialTransactions.AnyAsync(p => p.IdempotencyKey == key, ct);

    public async Task<FinancialTransaction> GetPaymentSnapshotAsync(Guid orderId, Guid paymentId, CancellationToken ct) =>
        await context.FinancialTransactions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId &&
            p.OrderId == orderId && p.TransactionType == FinancialTransactionType.Payment, ct)
        ?? throw new NotFoundException(nameof(FinancialTransaction), paymentId);
}
