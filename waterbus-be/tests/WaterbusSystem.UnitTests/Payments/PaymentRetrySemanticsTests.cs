using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Payments;

/// <summary>
/// Task 6 – ledger C4 (BR-PAY-03): tái hiện ngữ nghĩa "retry thanh toán" hiện tại để có bằng chứng thay vì suy đoán.
/// Kết luận được khoá bằng test: vnp_TxnRef KHÔNG bao giờ được dùng lại sau khi attempt đã có kết quả; mở lại URL khi attempt
/// còn Requested dùng cùng attempt (cùng TxnRef, cố ý); thử lại sau thất bại = checkout mới (Order/attempt/TxnRef mới).
/// </summary>
public sealed class PaymentRetrySemanticsTests
{
    [Fact]
    public async Task ReopeningPaymentBeforeAnyOutcome_ReusesTheSameAttemptAndTxnRef_ByDesign()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await CommercialHandlerFactory.Booking(context).CreateAsync(CommercialCheckoutTests.Command(seed));
        var payments = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        var first = await payments.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var second = await payments.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        Assert.Equal(first.MerchantReference, second.MerchantReference);
        Assert.Single(await context.FinancialTransactions.ToListAsync());
    }

    [Fact]
    public async Task AfterAFailedOutcome_TheTxnRefIsNeverReused_AndRetryNeedsANewCheckout()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        using var gateway = new VerifiedVnPayFixture();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await CommercialHandlerFactory.Booking(context).CreateAsync(CommercialCheckoutTests.Command(seed));
        var payments = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        var first = await payments.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var attempt = await context.FinancialTransactions.SingleAsync();
        Assert.Equal("00", (await payments.ConfirmIpnAsync(gateway.Callback(attempt.MerchantReference!, attempt.Amount, "24", "02"))).RspCode);
        Assert.Equal(FinancialTransactionStatus.Failed, attempt.Status);

        // cùng Order: không tạo attempt mới / không phát lại URL với TxnRef cũ (Order đã bị huỷ, ghế đã nhả)
        await Assert.ThrowsAsync<ConcurrencyException>(() => payments.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken));
        Assert.Single(await context.FinancialTransactions.ToListAsync());

        // thử lại = checkout mới → Order, attempt và TxnRef mới
        var retry = await CommercialHandlerFactory.Booking(context).CreateAsync(CommercialCheckoutTests.Command(seed));
        var second = await payments.CreateVnPayUrlAsync(retry.OrderId, "127.0.0.1", retry.AccessToken);
        Assert.NotEqual(first.MerchantReference, second.MerchantReference);
        Assert.NotEqual(booking.OrderId, retry.OrderId);
    }
}
