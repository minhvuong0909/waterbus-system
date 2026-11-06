using FluentAssertions;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Features.Notifications.Templates;
using WaterbusSystem.Domain.Enums;
using Xunit;

namespace WaterbusSystem.UnitTests.Notifications;

public sealed class NotificationTemplateTests
{
    private static readonly EmailContext Ctx = new("Smart Waterbus", "https://waterbus.example", null, null);
    private static readonly DateTimeOffset Departure = new(2026, 11, 7, 1, 30, 0, TimeSpan.Zero); // 08:30 giờ VN

    internal static BookingLeg Leg(string code = "WB-0001", string passenger = "Nguyen Van A", int passengers = 1, TripStatus trip = TripStatus.Scheduled,
        string from = "Ben Bach Dang", string to = "Ben Thanh Da") => new(
        Guid.NewGuid(), code, BookingStatus.Confirmed, 15000m * passengers, "pub-" + code, 1, Departure, Guid.NewGuid(), trip,
        "Tuyen Sai Gon", from, to, Departure, Departure.AddMinutes(30),
        Enumerable.Range(1, passengers).Select(i => new PassengerLine(i == 1 ? passenger : "Hanh khach " + i, "S0" + i, "Tieu chuan", $"TK{code}{i}", 15000m)).ToList());

    private static BookingConfirmationData Confirmation(params BookingLeg[] legs) => new(Guid.NewGuid(), "Nguyen Van A", "buyer@example.com",
        OrderStatus.Confirmed, legs.Length > 1 ? PurchaseMode.RoundTrip : PurchaseMode.OneWay, legs.Sum(l => l.PaidAmount), "VND", legs);

    private static Dictionary<Guid, string> Cids(BookingConfirmationData d) => d.Legs.ToDictionary(l => l.BookingId, l => $"qr-{l.BookingId:N}@waterbus");

    [Fact]
    public void ConfirmationEscapesEveryCustomerControlledField()
    {
        const string attack = "<script>alert(1)</script>\"><img src=x onerror=alert(2)>";
        var d = Confirmation(Leg(passenger: attack)) with { PurchaserName = attack };
        var r = NotificationTemplates.BookingConfirmed(d, Ctx, Cids(d));

        r.Html.Should().NotContain("<script").And.NotContain("onerror=alert(2)>").And.NotContain("<img src=x");
        r.Html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
        r.Text.Should().Contain(attack, "bản text thuần không cần escape HTML");
    }

    [Fact]
    public void ConfirmationShowsVndAmountsAndVietnamTime()
    {
        var d = Confirmation(Leg(passengers: 2));
        var r = NotificationTemplates.BookingConfirmed(d, Ctx, Cids(d));
        r.Html.Should().Contain("30.000 ₫"); // tổng
        r.Text.Should().Contain("08:30 ngày 07/11/2026 (GMT+7)");
        EmailFormat.Money(1234567m, "VND").Should().Be("1.234.567 ₫");
        EmailFormat.Money(0m, "VND").Should().Be("0 ₫");
        EmailFormat.Time(new DateTimeOffset(2026, 11, 7, 17, 5, 0, TimeSpan.Zero)).Should().Be("00:05 ngày 08/11/2026 (GMT+7)");
    }

    [Fact]
    public void ConfirmationListsEveryPassengerAndEveryLegOfARoundTrip()
    {
        var d = Confirmation(Leg("WB-OUT", passengers: 3), Leg("WB-BACK", passenger: "Tran Thi B", from: "Ben Thanh Da", to: "Ben Bach Dang"));
        var r = NotificationTemplates.BookingConfirmed(d, Ctx, Cids(d));

        r.Subject.Should().Contain("WB-OUT").And.Contain("WB-BACK");
        r.Html.Should().Contain("Khứ hồi").And.Contain("Chặng 1").And.Contain("Chặng 2");
        r.Html.Should().Contain("Hanh khach 2").And.Contain("Hanh khach 3").And.Contain("Tran Thi B");
        r.Html.Split("cid:qr-").Length.Should().Be(3, "mỗi Booking có đúng một ảnh QR (không phải mỗi Ticket)");
        r.Text.Should().Contain("Mã đơn hàng").And.Contain("WB-OUT, WB-BACK");
    }

    [Fact]
    public void ConfirmationNeverExposesGuestTokensOrOverpromisesCrossDeviceAccess()
    {
        var d = Confirmation(Leg());
        var r = NotificationTemplates.BookingConfirmed(d, Ctx, Cids(d));
        r.Html.Should().NotContainEquivalentOf("token").And.NotContainEquivalentOf("manage");
        r.Html.Should().Contain("https://waterbus.example/tickets").And.Contain("không tự mở được trên thiết bị khác");
    }

    [Fact]
    public void SupportChannelIsOnlyShownWhenGenuinelyConfigured()
    {
        var d = new TripDisruptionData(Guid.NewGuid(), "A", "a@example.com", "VND", Leg(trip: TripStatus.Cancelled));
        var without = NotificationTemplates.TripDisruption(d, false, Ctx);
        without.Html.Should().Contain("liên hệ bộ phận hỗ trợ Smart Waterbus").And.NotContain("@").And.NotContain("số ");
        var with = NotificationTemplates.TripDisruption(d, false, Ctx with { SupportEmail = "help@waterbus.example", SupportPhone = "1900 0000" });
        with.Html.Should().Contain("help@waterbus.example").And.Contain("1900 0000");
    }

    [Fact]
    public void CancellationAndTerminationHaveDistinctWordingAndNeverClaimARefundWasPaid()
    {
        var leg = Leg("WB-9", trip: TripStatus.Cancelled);
        var cancelled = NotificationTemplates.TripDisruption(new(Guid.NewGuid(), "A", "a@example.com", "VND", leg), false, Ctx);
        var terminated = NotificationTemplates.TripDisruption(new(Guid.NewGuid(), "A", "a@example.com", "VND", leg with { TripStatus = TripStatus.Terminated }), true, Ctx);

        cancelled.Subject.Should().Contain("bị hủy").And.Contain("WB-9");
        terminated.Subject.Should().Contain("chấm dứt giữa hành trình");
        cancelled.Html.Should().Contain("sẽ không khởi hành").And.NotContain("chấm dứt");
        terminated.Html.Should().Contain("chấm dứt giữa hành trình");
        foreach (var r in new[] { cancelled, terminated })
        {
            r.Html.Should().Contain("KHÔNG xác nhận rằng khoản hoàn tiền đã được thực hiện");
            r.Html.Should().NotContain("đã được hoàn").And.NotContain("Hoàn tiền thành công");
            r.Html.Should().Contain("Ben Bach Dang").And.Contain("Ben Thanh Da").And.Contain("08:30 ngày 07/11/2026");
        }
    }

    [Fact]
    public void RefundEmailShowsActualAmountPartialStatusAndNoInternalEvidence()
    {
        var leg = Leg("WB-RET", passengers: 2, trip: TripStatus.Cancelled);
        var allocations = leg.Passengers.Take(1).ToList();
        var partial = new RefundSuccessData(Guid.NewGuid(), Guid.NewGuid(), "A", "a@example.com", FinancialTransactionStatus.Succeeded,
            15000m, 30000m, "VND", "GW-123", Departure, leg, allocations);
        var r = NotificationTemplates.RefundSucceeded(partial, Ctx);

        r.Html.Should().Contain("15.000 ₫").And.Contain("Hoàn một phần").And.Contain("30.000 ₫").And.Contain("GW-123").And.Contain("WB-RET");
        r.Subject.Should().Contain("Hoàn tiền thành công");
        var full = NotificationTemplates.RefundSucceeded(partial with { Amount = 30000m, GatewayReference = null }, Ctx);
        full.Html.Should().Contain("Hoàn toàn bộ").And.NotContain("Mã giao dịch hoàn tiền");
    }

    [Fact]
    public void SubjectsAndHeadersContainNoLineBreaks()
    {
        EmailFormat.SingleLine("a\r\nBcc: evil@example.com").Should().NotContain("\n").And.NotContain("\r");
    }

    [Fact]
    public void AddressRulesRejectInjectionAndMalformedRecipients()
    {
        EmailAddressRules.IsValid("buyer@example.com").Should().BeTrue();
        foreach (var bad in new[] { "", " ", "a@b", "a b@example.com", "x@example.com\r\nBcc: y@example.com", "Name <a@example.com>", new string('a', 250) + "@example.com" })
            EmailAddressRules.IsValid(bad).Should().BeFalse(bad);
    }

    /// <summary>Đặt WATERBUS_WRITE_EMAIL_SAMPLES=&lt;thư mục&gt; để xuất bản render mẫu (docs/email-samples).</summary>
    [Fact]
    public void RendersExampleSamplesForAllThreeNotificationTypes()
    {
        var round = Confirmation(Leg("WB-20261107-A1", "Nguyen Van A", 2), Leg("WB-20261109-B2", "Nguyen Van A", 1, from: "Ben Thanh Da", to: "Ben Bach Dang"));
        var ctx = Ctx with { SupportEmail = "support@example.invalid", SupportPhone = "0000 000 000" };
        var samples = new Dictionary<string, NotificationTemplates.Rendered>
        {
            ["booking-confirmed"] = NotificationTemplates.BookingConfirmed(round, ctx, Cids(round)),
            ["trip-cancelled"] = NotificationTemplates.TripDisruption(new(Guid.NewGuid(), "Nguyen Van A", "a@example.com", "VND", round.Legs[1] with { TripStatus = TripStatus.Cancelled }), false, ctx),
            ["trip-terminated"] = NotificationTemplates.TripDisruption(new(Guid.NewGuid(), "Nguyen Van A", "a@example.com", "VND", round.Legs[0] with { TripStatus = TripStatus.Terminated }), true, ctx),
            ["refund-succeeded"] = NotificationTemplates.RefundSucceeded(new(Guid.NewGuid(), Guid.NewGuid(), "Nguyen Van A", "a@example.com",
                FinancialTransactionStatus.Succeeded, 15000m, 45000m, "VND", "14123456", Departure.AddDays(1), round.Legs[1] with { TripStatus = TripStatus.Cancelled }, round.Legs[1].Passengers), ctx)
        };
        foreach (var (name, r) in samples) { r.Html.Should().StartWith("<!DOCTYPE html>"); r.Text.Should().NotBeNullOrWhiteSpace(); r.Html.Should().NotContain("<script"); }
        var dir = Environment.GetEnvironmentVariable("WATERBUS_WRITE_EMAIL_SAMPLES");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        foreach (var (name, r) in samples)
        {
            File.WriteAllText(Path.Combine(dir, name + ".html"), r.Html);
            File.WriteAllText(Path.Combine(dir, name + ".txt"), $"Subject: {r.Subject}\n\n{r.Text}");
        }
    }
}
