using WaterbusSystem.Application.DTOs.Requests.Reports;
using WaterbusSystem.Application.DTOs.Responses.Reports;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Reports.Services;

/// <summary>
/// Báo cáo doanh thu theo tiền thực thu (BR-CFG-02): thực thu là Payment thành công, đã hoàn là Refund thành công,
/// thuần = thực thu − đã hoàn. Ghi nhận theo ngày hoàn tất giao dịch (giờ Việt Nam), không theo ngày đi tàu.
/// </summary>
public sealed class RevenueReportService(IRevenueReportRepository repository) : IRevenueReportService
{
    /// <summary>Asia/Ho_Chi_Minh, không có DST.</summary>
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<RevenueReportResponse> GetAsync(GetRevenueReportRequest request, CancellationToken ct = default)
    {
        var days = await repository.GetDailyAsync(StartOfDayUtc(request.From), StartOfDayUtc(request.To.AddDays(1)), ct);

        var byPeriod = days
            .GroupBy(d => PeriodStart(d.Date, request.Granularity))
            .ToDictionary(g => g.Key, g => (Collected: g.Sum(d => d.Collected), Refunded: g.Sum(d => d.Refunded)));

        var points = Periods(request)
            .Select(period =>
            {
                var (collected, refunded) = byPeriod.GetValueOrDefault(period);
                return new RevenuePointResponse(period, collected, refunded, collected - refunded);
            })
            .ToList();

        var collectedTotal = days.Sum(d => d.Collected);
        var refundedTotal = days.Sum(d => d.Refunded);
        var totals = new RevenueTotalsResponse(
            collectedTotal, refundedTotal, collectedTotal - refundedTotal,
            days.Sum(d => d.PaymentCount), days.Sum(d => d.RefundCount));

        return new RevenueReportResponse(request.From, request.To, request.Granularity.ToString(), totals, points);
    }

    private static DateTimeOffset StartOfDayUtc(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();

    private static DateOnly PeriodStart(DateOnly date, RevenueGranularity granularity) =>
        granularity == RevenueGranularity.Month ? new DateOnly(date.Year, date.Month, 1) : date;

    /// <summary>Mọi kỳ trong khoảng, kể cả kỳ không có giao dịch, để biểu đồ liên tục.</summary>
    private static IEnumerable<DateOnly> Periods(GetRevenueReportRequest request)
    {
        var current = PeriodStart(request.From, request.Granularity);
        while (current <= request.To)
        {
            yield return current;
            current = request.Granularity == RevenueGranularity.Month ? current.AddMonths(1) : current.AddDays(1);
        }
    }
}
