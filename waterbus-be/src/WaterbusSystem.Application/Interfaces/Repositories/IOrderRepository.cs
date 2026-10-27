using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IOrderRepository
{
    void Add(PurchaseOrder order);
    Task<PurchaseOrder> GetRequiredByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder> GetForSubmissionAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<bool> HasFinancialTransactionsAsync(Guid orderId, CancellationToken cancellationToken = default);
}
