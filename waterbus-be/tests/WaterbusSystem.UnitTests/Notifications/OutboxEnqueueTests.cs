using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using static WaterbusSystem.UnitTests.Notifications.NotificationScenario;

namespace WaterbusSystem.UnitTests.Notifications;

/// <summary>Outbox được tạo đúng tại các transition đã commit, đúng một lần, qua luồng nghiệp vụ thật (InMemory).</summary>
public sealed class OutboxEnqueueTests
{
    private static List<EmailOutboxMessage> Outbox(ApplicationDbContext c) => c.EmailOutbox.AsNoTracking().OrderBy(m => m.CreatedAt).ToList();

    // ---------------- EMAIL-01 ----------------

    [Fact]
    public async Task VerifiedPaymentCreatesExactlyOneConfirmationForPurchaserNotPassengers()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var order = await CheckoutAsync(c, g, "Nguyen Van A", "buyer@example.com", Leg(seed, seed.Seat1, seed.Seat2));
        Outbox(c).Should().BeEmpty("chưa thanh toán thì chưa có thông báo");

        var result = await PayAsync(c, g, order);

        result.RspCode.Should().Be("00");
        var message = Outbox(c).Should().ContainSingle().Subject;
        message.NotificationType.Should().Be(EmailNotificationType.BookingConfirmed);
        message.EventKey.Should().Be($"BOOKING_CONFIRMED:{order.OrderId:N}");
        message.RecipientEmail.Should().Be("buyer@example.com");
        message.RecipientName.Should().Be("Nguyen Van A");
        message.Status.Should().Be(EmailOutboxStatus.Pending);
        c.PurchaseOrders.Single().Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task RoundTripWithTwoTripsProducesOneNotificationForTheWholeOrder()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var back = AddReturnTrip(c, seed);
        var backSeed = seed with { Trip = back };
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1), Leg(backSeed, seed.Seat2));
        await PayAsync(c, g, order);

        Outbox(c).Should().ContainSingle(m => m.NotificationType == EmailNotificationType.BookingConfirmed);
        c.Bookings.Count(b => b.Status == BookingStatus.Confirmed).Should().Be(2);
    }

    [Fact]
    public async Task DuplicateIpnDoesNotCreateASecondLogicalNotification()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1));
        (await PayAsync(c, g, order)).RspCode.Should().Be("00");
        (await PayAsync(c, g, order)).RspCode.Should().Be("02");
        (await PayAsync(c, g, order)).RspCode.Should().Be("02");
        Outbox(c).Should().ContainSingle();
    }

    [Theory]
    [InlineData("02")] // giao dịch thất bại
    [InlineData("01")] // chưa hoàn tất
    public async Task FailedOrUnconfirmedPaymentNeverCreatesAConfirmation(string transactionStatus)
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1));
        await PayAsync(c, g, order, transactionStatus);
        Outbox(c).Should().BeEmpty();
        c.PurchaseOrders.Single().Status.Should().NotBe(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task LatePaymentNeedingReconciliationIsNotAConfirmedBooking()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1));
        c.PurchaseOrders.Single().ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await c.SaveChangesAsync();

        var result = await PayAsync(c, g, order);

        result.RspCode.Should().Be("00");
        c.FinancialTransactions.Single().FailureReason.Should().StartWith("ReconciliationRequired");
        c.Tickets.Should().BeEmpty();
        Outbox(c).Should().BeEmpty("thanh toán cần đối soát ≠ đơn đã xác nhận có vé");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("x y@example.com")]
    public async Task InvalidRecipientIsRecordedAsDeadLetterAndNeverBlocksThePayment(string email)
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1));
        c.PurchaseOrders.Single().PurchaserEmail = email; await c.SaveChangesAsync();

        (await PayAsync(c, g, order)).RspCode.Should().Be("00");

        c.PurchaseOrders.Single().Status.Should().Be(OrderStatus.Confirmed);
        var message = Outbox(c).Should().ContainSingle().Subject;
        message.Status.Should().Be(EmailOutboxStatus.DeadLetter);
        message.LastError.Should().Be("InvalidRecipient");
    }

    // ---------------- EMAIL-02 ----------------

    private static async Task<(TestDbContextFactory.TripSeedData Seed, Order A, Order B)> TwoPurchasersOnOneTrip(ApplicationDbContext c, CommercialCheckoutTests.Gateway g)
    {
        var seed = TestDbContextFactory.SeedTrip(c);
        var a = await CheckoutAsync(c, g, "Buyer A", "a@example.com", Leg(seed, seed.Seat1));
        var b = await CheckoutAsync(c, g, "Buyer B", "b@example.com", Leg(seed, seed.Seat2));
        await PayAsync(c, g, a); await PayAsync(c, g, b);
        return (seed, a, b);
    }

    [Fact]
    public async Task CancellingATripNotifiesEachAffectedPurchaserExactlyOnce()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var g = new CommercialCheckoutTests.Gateway();
        var (seed, a, b) = await TwoPurchasersOnOneTrip(c, g);
        c.EmailOutbox.RemoveRange(c.EmailOutbox); await c.SaveChangesAsync();

        await TripSvc(c).ChangeStatusAsync(seed.Trip.Id, Op(TripOperation.Cancel));

        var rows = Outbox(c);
        rows.Should().HaveCount(2).And.OnlyContain(m => m.NotificationType == EmailNotificationType.TripCancelled);
        rows.Select(m => m.RecipientEmail).Should().BeEquivalentTo("a@example.com", "b@example.com");
        rows.Select(m => m.EventKey).Should().OnlyHaveUniqueItems();

        // Lặp lại thao tác bị chặn bởi vòng đời Trip và không tạo thêm thông báo.
        await Assert.ThrowsAsync<ConflictException>(() => TripSvc(c).ChangeStatusAsync(seed.Trip.Id, Op(TripOperation.Cancel)));
        Outbox(c).Should().HaveCount(2);
    }

    [Fact]
    public async Task RoundTripCancellationOnlyNotifiesTheAffectedLeg()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var back = AddReturnTrip(c, seed);
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1), Leg(seed with { Trip = back }, seed.Seat2));
        await PayAsync(c, g, order);
        c.EmailOutbox.RemoveRange(c.EmailOutbox); await c.SaveChangesAsync();

        await TripSvc(c).ChangeStatusAsync(back.Id, Op(TripOperation.Cancel));

        var message = Outbox(c).Should().ContainSingle().Subject;
        var returnBooking = c.Bookings.Single(b => b.TripId == back.Id);
        message.BookingId.Should().Be(returnBooking.Id);
        message.EventKey.Should().Be($"TRIP_CANCELLED:{returnBooking.Id:N}");
        c.Bookings.Single(b => b.TripId == seed.Trip.Id).Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task TerminationIsADistinctNotificationType()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var g = new CommercialCheckoutTests.Gateway();
        var (seed, _, _) = await TwoPurchasersOnOneTrip(c, g);
        c.EmailOutbox.RemoveRange(c.EmailOutbox);
        seed.Trip.Status = TripStatus.EnRoute; seed.Trip.ActualDepartureTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        await c.SaveChangesAsync();

        await TripSvc(c).ChangeStatusAsync(seed.Trip.Id, Op(TripOperation.Terminate));

        Outbox(c).Should().HaveCount(2).And.OnlyContain(m => m.NotificationType == EmailNotificationType.TripTerminated && m.EventKey.StartsWith("TRIP_TERMINATED:"));
    }

    [Fact]
    public async Task NonDisruptiveTripTransitionsAndUnconfirmedBookingsCreateNothing()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1)); // chưa thanh toán
        await TripSvc(c).ChangeStatusAsync(seed.Trip.Id, Op(TripOperation.Suspend));
        await TripSvc(c).ChangeStatusAsync(seed.Trip.Id, Op(TripOperation.Resume));
        await TripSvc(c).ChangeStatusAsync(seed.Trip.Id, Op(TripOperation.Cancel));
        Outbox(c).Should().BeEmpty();
    }

    // ---------------- EMAIL-03 ----------------

    private static async Task<(Guid BookingId, Guid OrderId, CommercialCheckoutTests.Gateway Gateway)> PaidThenCancelled(
        ApplicationDbContext c, RefundGatewayResult outcome)
    {
        var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway { Outcome = outcome };
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1));
        await PayAsync(c, g, order);
        seed.Trip.Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        c.EmailOutbox.RemoveRange(c.EmailOutbox); await c.SaveChangesAsync();
        return (c.Bookings.Single().Id, order.OrderId, g);
    }

    [Fact]
    public async Task GatewayConfirmedRefundCreatesOneRefundNotification()
    {
        using var c = TestDbContextFactory.CreateDbContext();
        var (bookingId, _, g) = await PaidThenCancelled(c, RefundGatewayResult.Succeeded("refund-ref-1"));

        var refund = await CommercialHandlerFactory.Refund(c, [g]).RequestAsync(new(bookingId, "cancelled"));
        await CommercialHandlerFactory.Refund(c, [g]).RequestAsync(new(bookingId, "retry")); // lặp lại

        var message = Outbox(c).Should().ContainSingle().Subject;
        message.NotificationType.Should().Be(EmailNotificationType.RefundSucceeded);
        message.EventKey.Should().Be($"REFUND_SUCCEEDED:{refund.RefundTransactionId:N}");
        message.FinancialTransactionId.Should().Be(refund.RefundTransactionId);
        message.BookingId.Should().Be(bookingId);
    }

    [Theory]
    [InlineData(false)] // gateway timeout/đang xử lý
    [InlineData(true)]  // gateway từ chối ⇒ cần xử lý thủ công
    public async Task PendingOrFailedRefundNeverCreatesANotification(bool rejected)
    {
        using var c = TestDbContextFactory.CreateDbContext();
        var (bookingId, _, g) = await PaidThenCancelled(c, rejected ? RefundGatewayResult.Failed("declined") : RefundGatewayResult.Pending("ref"));
        var refund = await CommercialHandlerFactory.Refund(c, [g]).RequestAsync(new(bookingId, "cancelled"));
        refund.Status.Should().NotBe("Succeeded");
        Outbox(c).Should().BeEmpty();
    }

    [Fact]
    public async Task ReconciliationThatEventuallySucceedsNotifiesOnceEvenWhenRepeated()
    {
        using var c = TestDbContextFactory.CreateDbContext();
        var (bookingId, _, g) = await PaidThenCancelled(c, RefundGatewayResult.Pending("refund-bank-reference"));
        var refund = await CommercialHandlerFactory.Refund(c, [g]).RequestAsync(new(bookingId, "cancelled"));
        var payment = c.FinancialTransactions.Single(t => t.TransactionType == FinancialTransactionType.Payment);
        var service = new WaterbusSystem.Application.Features.Refunds.Services.RefundReconciliationService(new FinancialTransactionRepository(c),
            new EfUnitOfWork(c), g, new DirectCommercialTransactionRunner(), TestNotifications.Outbox(c));

        g.QueryResult = new() { IsSuccess = true, MerchantReference = payment.MerchantReference, TransactionType = "02",
            TransactionNo = "refund-bank-reference", TransactionStatus = "01", Amount = refund.Amount * 100 };
        (await service.ReconcileAsync(refund.RefundTransactionId, "127.0.0.1")).Should().Be("Processing");
        Outbox(c).Should().BeEmpty("gateway chưa xác nhận thành công");

        g.QueryResult = g.QueryResult with { TransactionStatus = "00" };
        (await service.ReconcileAsync(refund.RefundTransactionId, "127.0.0.1")).Should().Be("Succeeded");
        (await service.ReconcileAsync(refund.RefundTransactionId, "127.0.0.1")).Should().Be("Succeeded");

        Outbox(c).Should().ContainSingle(m => m.NotificationType == EmailNotificationType.RefundSucceeded);
    }

    [Fact]
    public async Task ManualRefundSuccessNotifiesOnceAndRepeatConfirmationIsRejected()
    {
        using var c = TestDbContextFactory.CreateDbContext();
        var (bookingId, _, g) = await PaidThenCancelled(c, RefundGatewayResult.Failed("manual required"));
        var refund = await CommercialHandlerFactory.Refund(c, [g]).RequestAsync(new(bookingId, "cancelled"));
        Outbox(c).Should().BeEmpty();

        var manual = CommercialHandlerFactory.ManualRefund(c, new AdminUser());
        await manual.ConfirmAsync(new(refund.RefundTransactionId, "bank receipt", "manual-ref", refund.Amount));
        await Assert.ThrowsAsync<ConcurrencyException>(() => manual.ConfirmAsync(new(refund.RefundTransactionId, "bank receipt", null, refund.Amount)));

        Outbox(c).Should().ContainSingle(m => m.EventKey == $"REFUND_SUCCEEDED:{refund.RefundTransactionId:N}");
    }

    [Fact]
    public async Task RoundTripRefundNotificationIsScopedToTheRefundedBooking()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var g = new CommercialCheckoutTests.Gateway { Outcome = RefundGatewayResult.Succeeded("refund-ref") };
        var back = AddReturnTrip(c, seed);
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1), Leg(seed with { Trip = back }, seed.Seat2));
        await PayAsync(c, g, order);
        back.Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        c.EmailOutbox.RemoveRange(c.EmailOutbox); await c.SaveChangesAsync();
        var returnBookingId = c.Bookings.Single(b => b.TripId == back.Id).Id;

        var refund = await CommercialHandlerFactory.Refund(c, [g]).RequestAsync(new(returnBookingId, "cancelled"));

        var message = Outbox(c).Should().ContainSingle().Subject;
        message.BookingId.Should().Be(returnBookingId);
        refund.Amount.Should().Be(15000m); // một phần của khoản thanh toán 30.000
    }

    private sealed class AdminUser : WaterbusSystem.Application.Common.Interfaces.ICurrentUserService
    {
        public string? UserId => "00000000-0000-0000-0000-000000000001";
        public string? UserRole => "Admin";
    }
}
