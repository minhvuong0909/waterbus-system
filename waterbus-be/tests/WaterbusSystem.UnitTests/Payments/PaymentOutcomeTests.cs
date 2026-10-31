using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Payments;

public sealed class PaymentOutcomeTests
{
    private static async Task<(BookingResponse Booking, FinancialTransaction Payment)> PendingAsync(
        ApplicationDbContext context, VerifiedVnPayFixture gateway)
    {
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await CommercialHandlerFactory.Booking(context).CreateAsync(CommercialCheckoutTests.Command(seed));
        await CommercialHandlerFactory.Payment(context, gateway.Gateway)
            .CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var payment = await context.FinancialTransactions.SingleAsync();
        gateway.QueryAmount = (long)(payment.Amount * 100);
        return (booking, payment);
    }

    [Theory]
    [InlineData("24", "02")]
    [InlineData("07", "07")]
    public async Task FailureThenVerifiedSuccessRecordsReceiptWithoutRestoringInventory(string response, string status)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var (booking, payment) = await PendingAsync(context, gateway);
        var service = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        Assert.Equal("00", (await service.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount, response, status))).RspCode);
        Assert.Equal(FinancialTransactionStatus.Failed, payment.Status);
        Assert.Equal("00", (await service.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount))).RspCode);
        Assert.Equal(FinancialTransactionStatus.Succeeded, payment.Status);
        Assert.StartsWith("ReconciliationRequired:", payment.FailureReason);
        Assert.Contains("VNPAY payment failed", payment.FailureReason);
        Assert.NotNull(payment.CompletedAt);
        Assert.Equal(OrderStatus.Expired, (await context.PurchaseOrders.SingleAsync()).Status);
        Assert.All(context.SeatReservations, r => Assert.Equal(ReservationStatus.Cancelled, r.Status));
        Assert.All(context.Bookings, b => Assert.Null(b.PaidAllocation));
        Assert.Empty(context.Tickets);
        Assert.Equal("02", (await service.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount))).RspCode);
        var refunds = new CommercialCheckoutTests.Gateway();
        var first = await CommercialHandlerFactory.Refund(context, [refunds]).RequestAsync(new(booking.BookingId, "Late verified receipt"));
        var retry = await CommercialHandlerFactory.Refund(context, [refunds]).RequestAsync(new(booking.BookingId, "Retry"));
        Assert.Equal(first.RefundTransactionId, retry.RefundTransactionId);
        Assert.Equal(payment.Amount, first.Amount);
        Assert.Equal(1, refunds.RefundCalls);
    }

    [Fact]
    public async Task DuplicateFailureDoesNotChangeTheRecordedOutcome()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var (_, payment) = await PendingAsync(context, gateway);
        var service = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        var callback = gateway.Callback(payment.MerchantReference!, payment.Amount, "24", "02");
        Assert.Equal("00", (await service.ConfirmIpnAsync(callback)).RspCode);
        var processedAt = payment.ProcessedAt;
        Assert.Equal("02", (await service.ConfirmIpnAsync(callback)).RspCode);
        Assert.Equal(processedAt, payment.ProcessedAt);
        Assert.Equal(FinancialTransactionStatus.Failed, payment.Status);
        Assert.Single(context.FinancialTransactions);
        Assert.Empty(context.Tickets);
    }

    [Fact]
    public async Task DuplicateSuccessDoesNotAllocateOrIssueAgain()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var (_, payment) = await PendingAsync(context, gateway);
        var service = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        var callback = gateway.Callback(payment.MerchantReference!, payment.Amount);
        Assert.Equal("00", (await service.ConfirmIpnAsync(callback)).RspCode);
        var completedAt = payment.CompletedAt;
        Assert.Equal("02", (await service.ConfirmIpnAsync(callback)).RspCode);
        Assert.Equal(completedAt, payment.CompletedAt);
        Assert.Single(context.Tickets);
        Assert.Single(context.FinancialTransactions);
        Assert.Equal(payment.Amount, context.Bookings.Single().PaidAllocation);
    }

    [Theory]
    [InlineData("24", "02", "14000111")]
    [InlineData("00", "00", "14000222")]
    public async Task ConflictingOutcomePreservesSettledReceiptAndTickets(string response, string status, string transactionNo)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var (_, payment) = await PendingAsync(context, gateway);
        var service = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        Assert.Equal("00", (await service.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount))).RspCode);
        var completedAt = payment.CompletedAt;
        var conflict = gateway.Callback(payment.MerchantReference!, payment.Amount, response, status, transactionNo);
        Assert.Equal("99", (await service.ConfirmIpnAsync(conflict)).RspCode);
        Assert.Contains("VerifiedOutcomeConflict:", payment.FailureReason);
        var evidence = payment.FailureReason;
        Assert.Equal("99", (await service.ConfirmIpnAsync(conflict)).RspCode);
        Assert.Equal(evidence, payment.FailureReason);
        Assert.Equal(FinancialTransactionStatus.Succeeded, payment.Status);
        Assert.Equal("14000111", payment.GatewayReference);
        Assert.Equal(completedAt, payment.CompletedAt);
        Assert.Equal(OrderStatus.Confirmed, context.PurchaseOrders.Single().Status);
        Assert.Equal(payment.Amount, context.Bookings.Single().PaidAllocation);
        Assert.Single(context.Tickets);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QuerydrAfterFailureRequiresVerifiedSettlementEvidence(bool validSignature)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var (booking, payment) = await PendingAsync(context, gateway);
        var service = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        await service.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount, "24", "02"));
        gateway.SignQueryResponse = validSignature;
        var result = await service.ReconcileAsync(booking.OrderId, payment.Id, booking.AccessToken, "127.0.0.1");
        Assert.Equal(validSignature ? "00" : "99", result.RspCode);
        Assert.Equal(validSignature ? FinancialTransactionStatus.Succeeded : FinancialTransactionStatus.Failed, payment.Status);
        Assert.Equal(1, gateway.QueryCalls);
        Assert.Empty(context.Tickets);
        Assert.All(context.SeatReservations, r => Assert.Equal(ReservationStatus.Cancelled, r.Status));
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("reference")]
    [InlineData("type")]
    public async Task QuerydrMismatchesCannotChangeFailedPayment(string mismatch)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var (booking, payment) = await PendingAsync(context, gateway);
        var service = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        await service.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount, "24", "02"));
        if (mismatch == "amount") gateway.QueryAmount++;
        if (mismatch == "reference") gateway.QueryReference = "other-payment";
        if (mismatch == "type") gateway.QueryType = "02";
        Assert.Equal("99", (await service.ReconcileAsync(booking.OrderId, payment.Id, booking.AccessToken, "127.0.0.1")).RspCode);
        Assert.Equal(FinancialTransactionStatus.Failed, payment.Status);
        Assert.Empty(context.Tickets);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LateSuccessNeverRestoresExpiredOrCancelledInventory(bool cancelled)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var (booking, payment) = await PendingAsync(context, gateway);
        if (cancelled)
            await CommercialHandlerFactory.Checkout(context).CancelAsync(booking.OrderId, booking.AccessToken);
        else
        {
            context.PurchaseOrders.Single().ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await context.SaveChangesAsync();
        }
        var service = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        Assert.Equal("00", (await service.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount))).RspCode);
        Assert.Equal(FinancialTransactionStatus.Succeeded, payment.Status);
        Assert.StartsWith("ReconciliationRequired:", payment.FailureReason);
        Assert.Equal(cancelled ? OrderStatus.Cancelled : OrderStatus.Expired, context.PurchaseOrders.Single().Status);
        Assert.All(context.SeatReservations, r => Assert.Equal(ReservationStatus.Cancelled, r.Status));
        Assert.Empty(context.Tickets);
    }

    [Theory]
    [InlineData("signature", "97")]
    [InlineData("merchant", "99")]
    [InlineData("amount", "04")]
    public async Task InvalidSuccessCannotOverrideFailure(string mismatch, string expected)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var (_, payment) = await PendingAsync(context, gateway);
        var service = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        await service.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount, "24", "02"));
        var callback = gateway.Callback(payment.MerchantReference!, payment.Amount);
        if (mismatch == "signature") callback["vnp_SecureHash"] = "invalid";
        if (mismatch == "merchant") callback["vnp_TmnCode"] = "OTHER";
        if (mismatch == "amount") callback["vnp_Amount"] = "1";
        if (mismatch != "signature") gateway.SignCallback(callback);
        Assert.Equal(expected, (await service.ConfirmIpnAsync(callback)).RspCode);
        Assert.Equal(FinancialTransactionStatus.Failed, payment.Status);
        Assert.Empty(context.Tickets);
    }
}
