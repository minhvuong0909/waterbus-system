using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using static WaterbusSystem.UnitTests.Notifications.NotificationScenario;

namespace WaterbusSystem.UnitTests.Notifications;

/// <summary>Composer + read-model thật (EF) trên dữ liệu sinh ra từ luồng nghiệp vụ thật: nội dung phải khớp dữ liệu đã commit.</summary>
public sealed class ComposerWithRealDataTests
{
    private sealed class Signer : IBookingQrSigner
    {
        public BookingQrCredential Sign(string id, int v, Guid trip, DateTimeOffset at) => new("signed:" + id, "RS256", "k1", v);
        public BookingQrPublicKey GetPublicKey() => new("k1", "RS256", "pem");
    }
    private sealed class Qr : IQrImageRenderer { public byte[] RenderPng(string payload) => [1, 2, 3]; }

    private static NotificationEmailComposer Composer(ApplicationDbContext c) => new(new NotificationDataRepository(c), new Signer(), new Qr(),
        Options.Create(new EmailOptions { Enabled = true, FromAddress = "no-reply@x.example", PublicBaseUrl = "https://waterbus.example", SupportEmail = "help@waterbus.example" }));

    private static Task<ComposeResult> Compose(ApplicationDbContext c, Func<EmailOutboxMessage, bool> pick) =>
        Composer(c).ComposeAsync(c.EmailOutbox.AsNoTracking().ToList().Single(pick), default);

    [Fact]
    public async Task RoundTripConfirmationDescribesBothLegsAllPassengersAndOneQrPerBooking()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var back = AddReturnTrip(c, seed);
        var order = await CheckoutAsync(c, g, "Nguyen <Van> A", "buyer@example.com", Leg(seed, seed.Seat1, seed.Seat2), Leg(seed with { Trip = back }, seed.Seat1));
        await PayAsync(c, g, order);

        var result = await Compose(c, m => m.NotificationType == EmailNotificationType.BookingConfirmed);

        var email = result.Email!;
        email.ToEmail.Should().Be("buyer@example.com");
        email.Images.Should().HaveCount(2, "một QR cho mỗi Booking, không phải mỗi Ticket");
        foreach (var code in c.Bookings.Select(b => b.BookingCode)) email.Subject.Should().Contain(code);
        email.HtmlBody.Should().Contain("Khứ hồi").And.Contain("Hanh khach 1").And.Contain("Hanh khach 2").And.Contain("Chặng 2")
            .And.Contain("Nguyen &lt;Van&gt; A").And.NotContain("<Van>");
        c.Tickets.Select(t => t.TicketCode).ToList().ForEach(code => email.TextBody.Should().Contain(code));
        email.TextBody.Should().Contain("Ben A").And.Contain("Ben C").And.Contain("45.000 ₫");
    }

    [Fact]
    public async Task CancellingTheReturnTripDescribesOnlyTheReturnLeg()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        var back = AddReturnTrip(c, seed);
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1), Leg(seed with { Trip = back }, seed.Seat2));
        await PayAsync(c, g, order);
        await TripSvc(c).ChangeStatusAsync(back.Id, Op(TripOperation.Cancel));

        var email = (await Compose(c, m => m.NotificationType == EmailNotificationType.TripCancelled)).Email!;

        var outbound = c.Bookings.Single(b => b.TripId == seed.Trip.Id); var returning = c.Bookings.Single(b => b.TripId == back.Id);
        email.Subject.Should().Contain(returning.BookingCode).And.NotContain(outbound.BookingCode);
        email.TextBody.Should().Contain(returning.BookingCode).And.NotContain(outbound.BookingCode);
        email.HtmlBody.Should().Contain("help@waterbus.example").And.Contain("KHÔNG xác nhận rằng khoản hoàn tiền đã được thực hiện");
        email.Images.Should().BeEmpty("email huỷ chuyến không chứa QR");
    }

    [Fact]
    public async Task ConfirmationIsSkippedIfTheOrderLaterStopsBeingConfirmed()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c); var g = new CommercialCheckoutTests.Gateway();
        await PayAsync(c, g, await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1)));
        c.PurchaseOrders.Single().Status = OrderStatus.Cancelled; await c.SaveChangesAsync();
        (await Compose(c, m => true)).SkipReason.Should().Contain("Confirmed");
    }

    [Fact]
    public async Task PartialRoundTripRefundNamesTheRefundedBookingAndMarksItPartial()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var g = new CommercialCheckoutTests.Gateway { Outcome = RefundGatewayResult.Succeeded("gw-refund-77") };
        var back = AddReturnTrip(c, seed);
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1), Leg(seed with { Trip = back }, seed.Seat2));
        await PayAsync(c, g, order);
        back.Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        var returning = c.Bookings.Single(b => b.TripId == back.Id);
        await CommercialHandlerFactory.Refund(c, [g]).RequestAsync(new(returning.Id, "cancelled"));

        var email = (await Compose(c, m => m.NotificationType == EmailNotificationType.RefundSucceeded)).Email!;

        email.Subject.Should().Contain("Hoàn tiền thành công").And.Contain(returning.BookingCode);
        email.HtmlBody.Should().Contain("Hoàn một phần").And.Contain("gw-refund-77").And.Contain("15.000 ₫").And.Contain("30.000 ₫");
        email.HtmlBody.Should().NotContain(c.Bookings.Single(b => b.TripId == seed.Trip.Id).BookingCode);
    }

    [Fact]
    public async Task LatePaymentFullRefundWithoutTicketsStillGetsAnAccurateEmail()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var g = new CommercialCheckoutTests.Gateway { Outcome = RefundGatewayResult.Succeeded("gw-late-1") };
        var order = await CheckoutAsync(c, g, "Buyer", "buyer@example.com", Leg(seed, seed.Seat1));
        c.PurchaseOrders.Single().ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await c.SaveChangesAsync();
        await PayAsync(c, g, order);
        c.EmailOutbox.Should().BeEmpty();

        await CommercialHandlerFactory.Refund(c, [g]).RequestAsync(new(c.Bookings.Single().Id, "late payment"));

        var email = (await Compose(c, m => m.NotificationType == EmailNotificationType.RefundSucceeded)).Email!;
        email.HtmlBody.Should().Contain("Hoàn toàn bộ số tiền đã thanh toán").And.Contain("không có vé nào được phát hành").And.Contain("gw-late-1");
    }
}
