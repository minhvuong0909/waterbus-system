namespace WaterbusSystem.Application.DTOs.Requests.Reports;

/// <summary>Đơn vị gộp của báo cáo doanh thu.</summary>
public enum RevenueGranularity
{
    Day = 0,
    Month = 1
}

/// <summary>
/// Báo cáo doanh thu cho Admin. <c>From</c>/<c>To</c> là ngày theo giờ Việt Nam (gồm cả hai đầu).
/// </summary>
public record GetRevenueReportRequest(DateOnly From, DateOnly To, RevenueGranularity Granularity = RevenueGranularity.Day);
