namespace WaterbusSystem.Application.DTOs.Responses.Reports;

/// <summary>Một kỳ của báo cáo. <c>PeriodStart</c> là ngày (theo giờ Việt Nam) hoặc ngày 1 của tháng.</summary>
public record RevenuePointResponse(DateOnly PeriodStart, decimal Collected, decimal Refunded, decimal Net);

/// <summary>Tổng cả khoảng thời gian: tiền thực thu, đã hoàn và thuần (thực thu trừ đã hoàn), kèm số giao dịch.</summary>
public record RevenueTotalsResponse(decimal Collected, decimal Refunded, decimal Net, int PaymentCount, int RefundCount);

public record RevenueReportResponse(
    DateOnly From,
    DateOnly To,
    string Granularity,
    RevenueTotalsResponse Totals,
    IReadOnlyList<RevenuePointResponse> Points);
