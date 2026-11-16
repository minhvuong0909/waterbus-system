using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class RevenueReportRepository(ApplicationDbContext context) : IRevenueReportRepository
{
    private sealed record DayTotal(DateOnly Date, decimal Amount, int Count);

    public async Task<IReadOnlyList<RevenueDay>> GetDailyAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        var succeeded = context.FinancialTransactions.AsNoTracking()
            .Where(t => t.Status == FinancialTransactionStatus.Succeeded && t.CompletedAt >= fromUtc && t.CompletedAt < toUtc);

        var payments = await DailyTotalsAsync(
            succeeded.Where(t => t.TransactionType == FinancialTransactionType.Payment)
                .Where(FinancialTransactionFilters.Not(FinancialTransactionFilters.IsLateReceipt)), ct);
        var refunds = await DailyTotalsAsync(
            succeeded.Where(t => t.TransactionType == FinancialTransactionType.Refund && !t.IsDemoRefund), ct);

        var paymentByDate = payments.ToDictionary(x => x.Date);
        var refundByDate = refunds.ToDictionary(x => x.Date);
        return paymentByDate.Keys.Union(refundByDate.Keys).Order()
            .Select(date =>
            {
                paymentByDate.TryGetValue(date, out var payment);
                refundByDate.TryGetValue(date, out var refund);
                return new RevenueDay(date, payment?.Amount ?? 0m, payment?.Count ?? 0, refund?.Amount ?? 0m, refund?.Count ?? 0);
            })
            .ToList();
    }

    /// <summary>Gộp theo ngày giờ Việt Nam ngay trong SQL (cộng 7 giờ vào mốc UTC rồi lấy ngày).</summary>
    private static async Task<List<DayTotal>> DailyTotalsAsync(IQueryable<FinancialTransaction> transactions, CancellationToken ct)
    {
        var rows = await transactions
            .Select(t => new { Local = t.CompletedAt!.Value.AddHours(7), t.Amount })
            .GroupBy(x => new { x.Local.Year, x.Local.Month, x.Local.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Amount = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync(ct);
        return rows.Select(r => new DayTotal(new DateOnly(r.Year, r.Month, r.Day), r.Amount, r.Count)).ToList();
    }
}
