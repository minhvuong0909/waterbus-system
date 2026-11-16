namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Tiền thực thu và đã hoàn của một ngày (theo giờ Việt Nam).</summary>
public record RevenueDay(DateOnly Date, decimal Collected, int PaymentCount, decimal Refunded, int RefundCount);

public interface IRevenueReportRepository
{
    /// <summary>
    /// Chỉ đọc. Gộp theo ngày giờ Việt Nam các giao dịch <c>Succeeded</c> có <c>CompletedAt</c> trong [fromUtc, toUtc).
    /// Payment: loại khoản tiền về muộn cần đối soát (chưa có vé). Refund: loại refund mô phỏng cũ (<c>IsDemoRefund</c>, không có tiền thật).
    /// Chỉ trả những ngày có giao dịch.
    /// </summary>
    Task<IReadOnlyList<RevenueDay>> GetDailyAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);
}
