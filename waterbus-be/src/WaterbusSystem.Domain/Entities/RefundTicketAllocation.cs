using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Chi tiết phân bổ hoàn tiền cho từng Ticket trong một lần refund.
/// Unique (RefundTransactionId, TicketId) prevents duplicates inside one refund.
/// Cross-refund limits must be checked transactionally against prior Requested/Processing/Succeeded refunds.
/// </summary>
public class RefundTicketAllocation : BaseEntity
{
    public Guid RefundTransactionId { get; set; }
    public FinancialTransaction? RefundTransaction { get; set; }
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public decimal Amount { get; set; }
}
