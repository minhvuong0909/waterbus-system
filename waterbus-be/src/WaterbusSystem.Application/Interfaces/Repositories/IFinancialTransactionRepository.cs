using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IFinancialTransactionRepository
{
    void Add(FinancialTransaction transaction);
    Task<FinancialTransaction?> GetRequestedPaymentAsync(Guid orderId, CancellationToken ct);
    Task<bool> HasPaymentAsync(Guid orderId, CancellationToken ct);
    Task<FinancialTransaction?> GetVnPayPaymentAsync(string merchantReference, CancellationToken ct);
    Task<bool> HasIdempotencyKeyAsync(string key, CancellationToken ct);
    Task<FinancialTransaction> GetPaymentSnapshotAsync(Guid orderId, Guid paymentId, CancellationToken ct);
    Task<FinancialTransaction?> GetActiveRefundForBookingAsync(Guid bookingId, CancellationToken ct);
    Task<FinancialTransaction?> GetSuccessfulPaymentForRefundAsync(Guid orderId, bool hasTickets, CancellationToken ct);
    Task<List<Guid>> GetAllocatedRefundTicketIdsAsync(Guid orderId, CancellationToken ct);
    Task<decimal> GetCommittedRefundTotalAsync(Guid paymentId, CancellationToken ct);
    Task<FinancialTransaction?> GetRefundForPaymentAsync(Guid paymentId, CancellationToken ct);
    Task<FinancialTransaction> GetRefundForManualConfirmationAsync(Guid refundId, CancellationToken ct);
    Task<FinancialTransaction> GetRefundSnapshotAsync(Guid refundId, CancellationToken ct);
    Task<FinancialTransaction> GetRefundForUpdateAsync(Guid refundId, CancellationToken ct);
    /// <summary>Danh sách chỉ đọc các khoản cần Admin đối soát (tiền về muộn + refund VNPAY còn Processing), mới nhất trước.</summary>
    Task<WaterbusSystem.Application.Common.Models.PaginatedList<WaterbusSystem.Application.DTOs.Responses.Refunds.ReconciliationItemResponse>> ListReconciliationAsync(int page, int pageSize, CancellationToken ct);

}
