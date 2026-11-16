using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Reports;
using WaterbusSystem.Application.DTOs.Responses.Reports;
using WaterbusSystem.Application.Features.Reports.Services;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Validators.Reports;
using Xunit;

namespace WaterbusSystem.UnitTests.Reports;

public sealed class RevenueReportServiceTests
{
    private sealed class FakeRepository(params RevenueDay[] days) : IRevenueReportRepository
    {
        public DateTimeOffset? FromUtc { get; private set; }
        public DateTimeOffset? ToUtc { get; private set; }

        public Task<IReadOnlyList<RevenueDay>> GetDailyAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
        {
            FromUtc = fromUtc;
            ToUtc = toUtc;
            return Task.FromResult<IReadOnlyList<RevenueDay>>(days);
        }
    }

    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public async Task Daily_FillsEmptyDaysAndComputesNet()
    {
        var repository = new FakeRepository(new RevenueDay(D(11, 2), 300000m, 3, 100000m, 1));
        var report = await new RevenueReportService(repository).GetAsync(new GetRevenueReportRequest(D(11, 1), D(11, 3)));

        report.Points.Select(p => p.PeriodStart).Should().Equal(D(11, 1), D(11, 2), D(11, 3));
        report.Points.Select(p => p.Net).Should().Equal(0m, 200000m, 0m);
        report.Totals.Should().Be(new RevenueTotalsResponse(300000m, 100000m, 200000m, 3, 1));
        report.Granularity.Should().Be("Day");
    }

    [Fact]
    public async Task Daily_QueriesTheVietnamDayRangeAsUtc()
    {
        var repository = new FakeRepository();
        await new RevenueReportService(repository).GetAsync(new GetRevenueReportRequest(D(11, 16), D(11, 16)));

        // 16/11 00:00 +07 = 15/11 17:00 UTC; đầu mút trên là 17/11 00:00 +07 (không gồm).
        repository.FromUtc.Should().Be(new DateTimeOffset(2026, 11, 15, 17, 0, 0, TimeSpan.Zero));
        repository.ToUtc.Should().Be(new DateTimeOffset(2026, 11, 16, 17, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Monthly_GroupsDaysIntoMonthsAndKeepsEmptyMonths()
    {
        var repository = new FakeRepository(
            new RevenueDay(D(10, 5), 100000m, 1, 0m, 0),
            new RevenueDay(D(10, 20), 50000m, 1, 20000m, 1),
            new RevenueDay(D(12, 1), 70000m, 1, 0m, 0));
        var report = await new RevenueReportService(repository)
            .GetAsync(new GetRevenueReportRequest(D(10, 3), D(12, 31), RevenueGranularity.Month));

        report.Points.Select(p => p.PeriodStart).Should().Equal(D(10, 1), D(11, 1), D(12, 1));
        report.Points.Select(p => p.Collected).Should().Equal(150000m, 0m, 70000m);
        report.Points.Select(p => p.Refunded).Should().Equal(20000m, 0m, 0m);
        report.Totals.Net.Should().Be(200000m);
    }

    [Fact]
    public async Task NoTransactions_ReturnsZeroTotals()
    {
        var report = await new RevenueReportService(new FakeRepository()).GetAsync(new GetRevenueReportRequest(D(11, 1), D(11, 2)));

        report.Totals.Should().Be(new RevenueTotalsResponse(0m, 0m, 0m, 0, 0));
        report.Points.Should().HaveCount(2);
    }

    [Fact]
    public void Validator_AcceptsSingleDayAndFullYearRange()
    {
        var validator = new GetRevenueReportRequestValidator();
        validator.Validate(new GetRevenueReportRequest(D(11, 1), D(11, 1))).IsValid.Should().BeTrue();
        validator.Validate(new GetRevenueReportRequest(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_RejectsReversedRangeAndRangesOverOneYearAndUnknownGranularity()
    {
        var validator = new GetRevenueReportRequestValidator();
        validator.Validate(new GetRevenueReportRequest(D(11, 2), D(11, 1))).IsValid.Should().BeFalse();
        validator.Validate(new GetRevenueReportRequest(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2))).IsValid.Should().BeFalse();
        validator.Validate(new GetRevenueReportRequest(D(11, 1), D(11, 2), (RevenueGranularity)9)).IsValid.Should().BeFalse();
    }
}
