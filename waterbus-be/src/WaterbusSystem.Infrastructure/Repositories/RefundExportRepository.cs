using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class RefundExportRepository(ApplicationDbContext context) : IRefundExportRepository
{
    /// <summary>Tiền tố khóa idempotency của refund cho khoản tiền về muộn (không có vé).</summary>
    private const string LateRefundKeyPrefix = "LATE_REFUND_";

    public async Task<IReadOnlyList<RefundExportRow>> ListPendingManualAsync(Guid? tripId, int limit, CancellationToken ct)
    {
        var rows = await context.FinancialTransactions.AsNoTracking()
            // IsDemoRefund: refund mô phỏng cũ (chế độ Demo đã gỡ) KHÔNG BAO GIỜ được đưa cho nhân viên hoàn tiền thật.
            .Where(r => r.TransactionType == FinancialTransactionType.Refund && r.GatewayName == "Manual" && !r.IsDemoRefund &&
                (r.Status == FinancialTransactionStatus.Requested || r.Status == FinancialTransactionStatus.Processing) &&
                (tripId == null || r.RefundBooking!.TripId == tripId))
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                BookingCode = r.RefundBooking!.BookingCode,
                r.OrderId,
                r.Order!.PurchaserName,
                r.Order.PurchaserEmail,
                r.Order.PurchaserPhone,
                r.Amount,
                r.Currency,
                Tickets = r.RefundAllocations.Select(a => a.Ticket!.TicketCode).OrderBy(c => c).ToList(),
                RouteName = r.RefundBooking.Trip!.Route!.Name,
                r.RefundBooking.Trip.DepartureTime,
                TripStatus = r.RefundBooking.Trip.Status,
                IsLate = r.IdempotencyKey.StartsWith(LateRefundKeyPrefix),
                r.MerchantReference,
                PaymentReference = r.OriginalPayment!.GatewayReference,
                PaidAt = r.OriginalPayment.CompletedAt,
                r.Status,
                r.CreatedAt,
                r.ExportedAt
            })
            .ToListAsync(ct);

        return rows.Select(x => new RefundExportRow(
            x.Id, x.BookingCode, x.OrderId, x.PurchaserName, x.PurchaserEmail, x.PurchaserPhone,
            x.Amount, x.Currency, x.Tickets, x.RouteName, x.DepartureTime, x.IsLate, x.TripStatus.ToString(),
            x.MerchantReference, x.PaymentReference, x.PaidAt, x.Status.ToString(), x.CreatedAt, x.ExportedAt)).ToList();
    }

    public Task MarkExportedAsync(IReadOnlyCollection<Guid> refundIds, Guid adminId, DateTimeOffset exportedAt, CancellationToken ct) =>
        context.FinancialTransactions
            .Where(r => refundIds.Contains(r.Id) && r.TransactionType == FinancialTransactionType.Refund)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExportedAt, exportedAt).SetProperty(r => r.ExportedByAdminId, adminId), ct);
}
