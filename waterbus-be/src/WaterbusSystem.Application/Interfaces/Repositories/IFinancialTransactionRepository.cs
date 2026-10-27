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
}
