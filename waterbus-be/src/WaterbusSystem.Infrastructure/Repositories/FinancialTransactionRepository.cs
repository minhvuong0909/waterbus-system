using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class FinancialTransactionRepository(ApplicationDbContext context) : IFinancialTransactionRepository
{
    private sealed class ReconRow
    {
        public string Kind { get; set; } = "";
        public Guid TransactionId { get; set; }
        public Guid OrderId { get; set; }
        public Guid? BookingId { get; set; }
        public decimal Amount { get; set; }
        public string? MerchantReference { get; set; }
        public string? GatewayReference { get; set; }
        public FinancialTransactionStatus Status { get; set; }
        public string? Reason { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public Guid? RefundId { get; set; }
        public FinancialTransactionStatus? RefundStatus { get; set; }
    }

    public async Task<Application.Common.Models.PaginatedList<Application.DTOs.Responses.Refunds.ReconciliationItemResponse>> ListReconciliationAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var late = context.FinancialTransactions.AsNoTracking()
            .Where(p => p.TransactionType == FinancialTransactionType.Payment && p.Status == FinancialTransactionStatus.Succeeded &&
                p.FailureReason != null && (p.FailureReason.StartsWith("ReconciliationRequired:") || p.FailureReason.Contains("VerifiedOutcomeConflict")))
            .Select(p => new ReconRow { Kind = "LatePayment", TransactionId = p.Id, OrderId = p.OrderId,
                BookingId = p.Order!.Bookings.Select(b => (Guid?)b.Id).FirstOrDefault(), Amount = p.Amount, MerchantReference = p.MerchantReference,
                GatewayReference = p.GatewayReference, Status = p.Status, Reason = p.FailureReason, OccurredAt = p.CompletedAt ?? p.CreatedAt,
                RefundId = p.Refunds.Select(r => (Guid?)r.Id).FirstOrDefault(),
                RefundStatus = p.Refunds.Select(r => (FinancialTransactionStatus?)r.Status).FirstOrDefault() });
        var stuck = context.FinancialTransactions.AsNoTracking()
            .Where(r => r.TransactionType == FinancialTransactionType.Refund && r.Status == FinancialTransactionStatus.Processing && r.GatewayName == "VNPAY")
            .Select(r => new ReconRow { Kind = "RefundProcessing", TransactionId = r.Id, OrderId = r.OrderId, BookingId = r.RefundBookingId,
                Amount = r.Amount, MerchantReference = r.MerchantReference, GatewayReference = r.GatewayReference, Status = r.Status,
                Reason = r.FailureReason, OccurredAt = r.ProcessedAt ?? r.CreatedAt, RefundId = null, RefundStatus = null });
        var rows = await late.Concat(stuck).OrderByDescending(x => x.OccurredAt).ThenBy(x => x.TransactionId)
            .ToPaginatedListAsync(page, pageSize, ct);
        return new(rows.Items.Select(x => new Application.DTOs.Responses.Refunds.ReconciliationItemResponse(x.Kind, x.TransactionId, x.OrderId,
            x.BookingId, x.Amount, x.MerchantReference, x.GatewayReference, x.Status.ToString(), x.Reason, x.OccurredAt, x.RefundId,
            x.RefundStatus?.ToString())).ToList(), rows.TotalCount, rows.PageNumber, pageSize);
    }

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
    public Task<FinancialTransaction?> GetActiveRefundForBookingAsync(Guid bookingId, CancellationToken ct) =>
        context.FinancialTransactions.Include(r => r.RefundAllocations).ThenInclude(a => a.Ticket)
            .FirstOrDefaultAsync(r => r.RefundBookingId == bookingId && r.TransactionType == FinancialTransactionType.Refund &&
                (r.Status == FinancialTransactionStatus.Requested || r.Status == FinancialTransactionStatus.Processing ||
                 r.Status == FinancialTransactionStatus.Succeeded), ct);

    public Task<FinancialTransaction?> GetSuccessfulPaymentForRefundAsync(Guid orderId, bool hasTickets, CancellationToken ct) =>
        context.FinancialTransactions.Where(t => t.OrderId == orderId && t.TransactionType == FinancialTransactionType.Payment &&
            t.Status == FinancialTransactionStatus.Succeeded && (!hasTickets || t.FailureReason == null ||
                !t.FailureReason.StartsWith("ReconciliationRequired:")))
            .OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync(ct);

    public Task<List<Guid>> GetAllocatedRefundTicketIdsAsync(Guid orderId, CancellationToken ct) =>
        context.RefundTicketAllocations.AsNoTracking().Where(a => a.RefundTransaction != null &&
            a.RefundTransaction.OrderId == orderId && a.RefundTransaction.TransactionType == FinancialTransactionType.Refund &&
            (a.RefundTransaction.Status == FinancialTransactionStatus.Succeeded ||
             a.RefundTransaction.Status == FinancialTransactionStatus.Processing ||
             a.RefundTransaction.Status == FinancialTransactionStatus.Requested))
            .Select(a => a.TicketId).ToListAsync(ct);

    public async Task<decimal> GetCommittedRefundTotalAsync(Guid paymentId, CancellationToken ct) =>
        await context.FinancialTransactions.AsNoTracking().Where(r => r.OriginalPaymentId == paymentId &&
            r.TransactionType == FinancialTransactionType.Refund && (r.Status == FinancialTransactionStatus.Requested ||
                r.Status == FinancialTransactionStatus.Processing || r.Status == FinancialTransactionStatus.Succeeded))
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;

    public Task<FinancialTransaction?> GetRefundForPaymentAsync(Guid paymentId, CancellationToken ct) =>
        context.FinancialTransactions.Include(r => r.RefundAllocations)
            .FirstOrDefaultAsync(r => r.OriginalPaymentId == paymentId && r.TransactionType == FinancialTransactionType.Refund, ct);

    public async Task<FinancialTransaction> GetRefundForManualConfirmationAsync(Guid refundId, CancellationToken ct) =>
        await context.FinancialTransactions.Include(t => t.RefundAllocations).Include(t => t.OriginalPayment)
            .Include(t => t.RefundBooking).ThenInclude(b => b!.Tickets)
            .FirstOrDefaultAsync(t => t.Id == refundId && t.TransactionType == FinancialTransactionType.Refund, ct)
        ?? throw new NotFoundException(nameof(FinancialTransaction), refundId);

    public async Task<FinancialTransaction> GetRefundSnapshotAsync(Guid refundId, CancellationToken ct) =>
        await context.FinancialTransactions.AsNoTracking().Include(r => r.OriginalPayment)
            .FirstOrDefaultAsync(r => r.Id == refundId && r.TransactionType == FinancialTransactionType.Refund, ct)
        ?? throw new NotFoundException(nameof(FinancialTransaction), refundId);

    public async Task<FinancialTransaction> GetRefundForUpdateAsync(Guid refundId, CancellationToken ct)
    {
        var refund = await context.FinancialTransactions.FirstOrDefaultAsync(r => r.Id == refundId &&
            r.TransactionType == FinancialTransactionType.Refund, ct)
            ?? throw new NotFoundException(nameof(FinancialTransaction), refundId);
        await context.Entry(refund).ReloadAsync(ct);
        return refund;
    }

}
