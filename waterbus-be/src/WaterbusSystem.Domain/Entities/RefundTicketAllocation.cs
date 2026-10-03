using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>
/// Chi tiết phân bổ hoàn tiền cho từng Ticket trong một lần refund.
/// Unique (RefundTransactionId, TicketId) ngăn hoàn tiền 2 lần cùng Ticket.
/// </summary>
public class RefundTicketAllocation : BaseEntity
{
    public Guid RefundTransactionId { get; set; }
    public FinancialTransaction? RefundTransaction { get; set; }
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public decimal Amount { get; set; }
}
