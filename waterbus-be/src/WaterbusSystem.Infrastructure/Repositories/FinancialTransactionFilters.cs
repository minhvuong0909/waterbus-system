using System.Linq.Expressions;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Infrastructure.Repositories;

/// <summary>Điều kiện truy vấn <see cref="FinancialTransaction"/> dùng chung để không lệch nhau giữa các repository.</summary>
internal static class FinancialTransactionFilters
{
    /// <summary>
    /// Payment thành công nhưng tiền về muộn/xung đột kết quả: đã nhận tiền mà không có vé, Admin phải đối soát hoặc hoàn.
    /// Áp dụng sau khi đã lọc <c>TransactionType = Payment</c>.
    /// </summary>
    public static readonly Expression<Func<FinancialTransaction, bool>> IsLateReceipt = p =>
        p.FailureReason != null && (p.FailureReason.StartsWith("ReconciliationRequired:") || p.FailureReason.Contains("VerifiedOutcomeConflict"));

    public static Expression<Func<FinancialTransaction, bool>> Not(Expression<Func<FinancialTransaction, bool>> filter) =>
        Expression.Lambda<Func<FinancialTransaction, bool>>(Expression.Not(filter.Body), filter.Parameters);
}
