using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Payments;

public sealed class RefundTimeoutSafetyTests
{
    [Theory]
    [InlineData("01", "00")]
    [InlineData("02", "00")]
    [InlineData("03", "00")]
    [InlineData("03", "09")]
    [InlineData("03", "05")]
    public async Task UncorrelatedSignedQueryCannotSettleOrResendATimedOutRefund(string type, string status)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await CommercialHandlerFactory.Booking(context).CreateAsync(CommercialCheckoutTests.Command(seed));
        var paymentService = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        await paymentService.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var payment = await context.FinancialTransactions.SingleAsync();
        await paymentService.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount));
        seed.Trip.Status = TripStatus.Cancelled;
        await context.SaveChangesAsync();

        var timeout = new TimeoutGateway();
        var requests = CommercialHandlerFactory.Refund(context, [timeout]);
        var result = await requests.RequestAsync(new(booking.BookingId, "Trip cancelled"));
        var refund = await context.FinancialTransactions.SingleAsync(t => t.Id == result.RefundTransactionId);
        Assert.Equal(FinancialTransactionStatus.Processing, refund.Status);
        Assert.Null(refund.GatewayReference);
        Assert.Equal(refund.Id.ToString("N"), timeout.Request!.RequestId);
        Assert.Equal(payment.MerchantReference, timeout.Request.MerchantReference);

        gateway.QueryAmount = (long)(refund.Amount * 100);
        gateway.QueryType = type;
        gateway.QueryStatus = status;
        gateway.QueryTransactionNo = "unmatched-gateway-refund";
        var reconciliation = new RefundReconciliationService(new FinancialTransactionRepository(context),
            new EfUnitOfWork(context), gateway.Gateway, new DirectCommercialTransactionRunner());
        Assert.Equal("Processing", await reconciliation.ReconcileAsync(refund.Id, "127.0.0.1"));
        Assert.Equal(1, gateway.QueryCalls);
        Assert.Null(refund.CompletedAt);
        Assert.Null(refund.GatewayReference);
        await Assert.ThrowsAsync<ConcurrencyException>(() => CommercialHandlerFactory.ManualRefund(context, new Admin())
            .ConfirmAsync(new(refund.Id, "Unverified bank transfer", "manual-reference", refund.Amount)));

        var retry = await requests.RequestAsync(new(booking.BookingId, "Retry after timeout"));
        Assert.Equal(refund.Id, retry.RefundTransactionId);
        Assert.Equal(1, timeout.Calls);
        Assert.Equal(FinancialTransactionStatus.Processing, refund.Status);
        Assert.Equal(payment.Amount, refund.Amount);
        Assert.Equal(refund.Amount, await context.RefundTicketAllocations.SumAsync(a => a.Amount));
        Assert.Equal(2, await context.FinancialTransactions.CountAsync());
        Assert.Single(context.RefundTicketAllocations);
    }

    private sealed class TimeoutGateway : IRefundGateway
    {
        public string Name => "VNPAY";
        public bool SupportsAutomaticRefund => true;
        public int Calls { get; private set; }
        public RefundGatewayRequest? Request { get; private set; }

        public Task<RefundGatewayResult> RefundAsync(RefundGatewayRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Request = request;
            throw new TimeoutException("Refund response was lost.");
        }
    }

    private sealed class Admin : ICurrentUserService
    {
        public string? UserId => "00000000-0000-0000-0000-000000000001";
        public string? UserRole => "Admin";
    }
}
