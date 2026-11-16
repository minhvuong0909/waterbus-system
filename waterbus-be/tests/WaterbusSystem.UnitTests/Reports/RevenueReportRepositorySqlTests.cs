using FluentAssertions;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Domain;
using Xunit;

namespace WaterbusSystem.UnitTests.Reports;

/// <summary>Kiểm truy vấn gộp doanh thu trên SQL Server thật (cần WATERBUS_TEST_SQLSERVER); mỗi test rollback.</summary>
[Collection("Domain SQL Server")]
public class RevenueReportRepositorySqlTests(DomainSqlFixture fixture)
{
    private static readonly DateTimeOffset RangeFrom = new(2026, 11, 14, 17, 0, 0, TimeSpan.Zero); // 15/11 00:00 +07
    private static readonly DateTimeOffset RangeTo = new(2026, 11, 17, 17, 0, 0, TimeSpan.Zero);   // 18/11 00:00 +07

    private static FinancialTransaction Payment(PurchaseOrder order, decimal amount, DateTimeOffset completedAt,
        FinancialTransactionStatus status = FinancialTransactionStatus.Succeeded, string? failureReason = null) => new()
    {
        Order = order, Amount = amount, Status = status, CompletedAt = completedAt, FailureReason = failureReason,
        IdempotencyKey = Guid.NewGuid().ToString("N")
    };

    private static FinancialTransaction Refund(Booking booking, FinancialTransaction payment, decimal amount, DateTimeOffset completedAt,
        FinancialTransactionStatus status, bool demo) => new()
    {
        OrderId = booking.OrderId, RefundBookingId = booking.Id, OriginalPaymentId = payment.Id,
        TransactionType = FinancialTransactionType.Refund, Amount = amount, Status = status, CompletedAt = completedAt,
        IsDemoRefund = demo, IdempotencyKey = Guid.NewGuid().ToString("N")
    };

    [SqlServerDomainFact]
    public async Task GroupsByVietnamDayAndCountsOnlyRealMoney()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var (booking, _, _) = await DomainPersistenceTests.SeedJourney(context);
        context.Bookings.Add(booking);
        var order = booking.Order!;
        var onTheSixteenth = new DateTimeOffset(2026, 11, 16, 1, 0, 0, TimeSpan.Zero);

        // 15/11 23:00 UTC (= 16/11 06:00 +07) và 15/11 20:30 UTC (= 16/11 03:30 +07) cùng thuộc ngày 16/11 giờ Việt Nam.
        var paidA = Payment(order, 100000m, new DateTimeOffset(2026, 11, 15, 23, 0, 0, TimeSpan.Zero));
        var paidB = Payment(order, 50000m, new DateTimeOffset(2026, 11, 15, 20, 30, 0, TimeSpan.Zero));
        // 16/11 17:30 UTC = 17/11 00:30 +07: sang ngày 17 giờ Việt Nam dù vẫn là 16/11 theo UTC.
        var paidNextDay = Payment(order, 70000m, new DateTimeOffset(2026, 11, 16, 17, 30, 0, TimeSpan.Zero));
        context.FinancialTransactions.AddRange(
            paidA, paidB, paidNextDay,
            Payment(order, 999m, onTheSixteenth, FinancialTransactionStatus.Failed),
            Payment(order, 888m, onTheSixteenth, FinancialTransactionStatus.Processing),
            Payment(order, 777m, onTheSixteenth, failureReason: "ReconciliationRequired: late"),
            Payment(order, 666m, onTheSixteenth, failureReason: "VerifiedOutcomeConflict"),
            Payment(order, 555m, new DateTimeOffset(2026, 11, 20, 1, 0, 0, TimeSpan.Zero)));
        await context.SaveChangesAsync();

        context.FinancialTransactions.AddRange(
            Refund(booking, paidA, 40000m, onTheSixteenth, FinancialTransactionStatus.Succeeded, demo: false),
            Refund(booking, paidA, 11111m, onTheSixteenth, FinancialTransactionStatus.Succeeded, demo: true),
            Refund(booking, paidA, 22222m, onTheSixteenth, FinancialTransactionStatus.Processing, demo: false));
        await context.SaveChangesAsync();

        var days = await new RevenueReportRepository(context).GetDailyAsync(RangeFrom, RangeTo, default);

        days.Should().BeEquivalentTo(new[]
        {
            new RevenueDay(new DateOnly(2026, 11, 16), 150000m, 2, 40000m, 1),
            new RevenueDay(new DateOnly(2026, 11, 17), 70000m, 1, 0m, 0),
        });
    }

    [SqlServerDomainFact]
    public async Task EmptyRange_ReturnsNothing()
    {
        await using var context = fixture.CreateContext();
        await using var tx = await context.Database.BeginTransactionAsync();

        (await new RevenueReportRepository(context).GetDailyAsync(RangeFrom, RangeTo, default)).Should().BeEmpty();
    }
}
