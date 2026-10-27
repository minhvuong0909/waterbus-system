using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Application.Validators.Refunds;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Refunds;

public sealed class RefundServiceTests
{
    private static RefundRequestService Requests(ApplicationDbContext context, IRefundGateway gateway) => new(
        new FinancialTransactionRepository(context), new BookingRepository(context), new EfUnitOfWork(context),
        new RequestRefundRequestValidator(), [gateway], new DirectCommercialTransactionRunner());

    private static ManualRefundService Manual(ApplicationDbContext context) => new(new FinancialTransactionRepository(context),
        new EfUnitOfWork(context), new ConfirmManualRefundRequestValidator(), new Admin(), new DirectCommercialTransactionRunner());

    private sealed class Admin : ICurrentUserService
    {
        public string? UserId => "00000000-0000-0000-0000-000000000001";
        public string? UserRole => "Admin";
    }

    private static async Task<Guid> PaidBooking(ApplicationDbContext context, CommercialCheckoutTests.Gateway gateway)
    {
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await BookingApplicationServiceTests.Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var payment = CommercialHandlerFactory.Payment(context, gateway);
        await payment.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        await payment.ConfirmIpnAsync(CommercialCheckoutTests.Ipn(context.FinancialTransactions.Single()).Query);
        seed.Trip.Status = TripStatus.Cancelled;
        await context.SaveChangesAsync();
        return booking.BookingId;
    }

    [Fact]
    public async Task RequestValidationRunsBeforeCallingGateway()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var gateway = new CommercialCheckoutTests.Gateway();
        await Assert.ThrowsAsync<WaterbusSystem.Application.Common.Exceptions.ValidationException>(() =>
            Requests(context, gateway).RequestAsync(new(Guid.Empty, "")));
        Assert.Equal(0, gateway.RefundCalls);
        Assert.Empty(context.FinancialTransactions);
    }

    [Fact]
    public async Task DuplicateRequestsShareAllocationsAndGatewaySubmission()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var gateway = new CommercialCheckoutTests.Gateway();
        var bookingId = await PaidBooking(context, gateway);
        var service = Requests(context, gateway);
        var first = await service.RequestAsync(new(bookingId, "cancelled"));
        var second = await service.RequestAsync(new(bookingId, "retry"));
        Assert.Equal(first.RefundTransactionId, second.RefundTransactionId);
        Assert.Equal(first.Amount, Assert.Single(second.Tickets).Amount);
        Assert.Equal(1, gateway.RefundCalls);
        Assert.Equal("Processing", second.Status);
    }

    [Fact]
    public async Task ManualRefundRequiresEvidenceAndExactAmountAndCannotRepeat()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var gateway = new CommercialCheckoutTests.Gateway { Outcome = RefundGatewayResult.Failed("manual required") };
        var bookingId = await PaidBooking(context, gateway);
        var refund = await Requests(context, gateway).RequestAsync(new(bookingId, "cancelled"));
        var service = Manual(context);
        await Assert.ThrowsAsync<WaterbusSystem.Application.Common.Exceptions.ValidationException>(() =>
            service.ConfirmAsync(new(refund.RefundTransactionId, "", null, refund.Amount)));
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.ConfirmAsync(new(refund.RefundTransactionId, "bank receipt", null, refund.Amount + 1)));
        var result = await service.ConfirmAsync(new(refund.RefundTransactionId, " bank receipt ", "manual-bank-ref", refund.Amount));
        Assert.Equal("Succeeded", result.Status);
        var stored = context.FinancialTransactions.Single(t => t.Id == refund.RefundTransactionId);
        Assert.Equal("bank receipt", stored.ManualRefundEvidence);
        Assert.NotNull(stored.ManualProcessedByAdminId);
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.ConfirmAsync(new(refund.RefundTransactionId, "bank receipt", null, refund.Amount)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReconciliationRequiresRefundReferenceAndExactAmount(bool matching)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var gateway = new CommercialCheckoutTests.Gateway();
        var bookingId = await PaidBooking(context, gateway);
        var refund = await Requests(context, gateway).RequestAsync(new(bookingId, "cancelled"));
        var payment = context.FinancialTransactions.Single(t => t.TransactionType == FinancialTransactionType.Payment);
        gateway.QueryResult = new VnPayQueryResult
        {
            IsSuccess = true, MerchantReference = payment.MerchantReference, TransactionType = "02",
            TransactionNo = "refund-bank-reference", TransactionStatus = "00", Amount = refund.Amount * 100 + (matching ? 0 : 1)
        };
        var service = new RefundReconciliationService(new FinancialTransactionRepository(context), new EfUnitOfWork(context),
            gateway, new DirectCommercialTransactionRunner());
        Assert.Equal(matching ? "Succeeded" : "Processing", await service.ReconcileAsync(refund.RefundTransactionId, "127.0.0.1"));
    }

    [Fact]
    public async Task DelayedGatewayOutcomeCannotOverwriteReconciledRefund()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new ApplicationDbContext(options, null);
        var gateway = new CommercialCheckoutTests.Gateway();
        var bookingId = await PaidBooking(context, gateway);
        gateway.BeforeRefund = () =>
        {
            using var competing = new ApplicationDbContext(options, null);
            var refund = competing.FinancialTransactions.Single(t => t.TransactionType == FinancialTransactionType.Refund);
            refund.Status = FinancialTransactionStatus.Succeeded;
            refund.GatewayReference = "reconciled-reference";
            refund.CompletedAt = DateTimeOffset.UtcNow;
            competing.SaveChanges();
        };
        var result = await Requests(context, gateway).RequestAsync(new(bookingId, "cancelled"));
        Assert.Equal("Succeeded", result.Status);
        using var verify = new ApplicationDbContext(options, null);
        var stored = verify.FinancialTransactions.Single(t => t.Id == result.RefundTransactionId);
        Assert.Equal(FinancialTransactionStatus.Succeeded, stored.Status);
        Assert.Equal("reconciled-reference", stored.GatewayReference);
    }
}
