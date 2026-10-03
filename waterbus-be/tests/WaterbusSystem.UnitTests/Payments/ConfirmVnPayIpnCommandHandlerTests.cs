using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services.Payment;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Payments;

/// <summary>
/// Test thật cho luồng xử lý Webhook IPN VNPAY (ConfirmVnPayIpnCommandHandler), dùng VnPayService thật
/// để tự ký request IPN giả lập (đóng vai máy chủ VNPAY) rồi gửi qua handler như một webhook thật.
/// </summary>
public class ConfirmVnPayIpnCommandHandlerTests
{
    private const string TmnCode = "TESTCODE01";
    private const string HashSecret = "TEST_SECRET_KEY_FOR_UNIT_TEST_ONLY";

    private static VnPayService CreateVnPayService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VnPay:TmnCode"] = TmnCode,
                ["VnPay:HashSecret"] = HashSecret
            })
            .Build();

        return new VnPayService(configuration);
    }

    private static Booking SeedPendingBooking(Infrastructure.Persistence.ApplicationDbContext context, decimal totalAmount)
    {
        var seedData = TestDbContextFactory.SeedTrip(context);

        var booking = new Booking
        {
            BookingCode = "WB20261002TEST01",
            CustomerName = "Nguyen Van A",
            CustomerEmail = "a@test.com",
            CustomerPhone = "0901234567",
            Status = BookingStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            TotalAmount = totalAmount
        };
        var reservation = new SeatReservation
        {
            TripId = seedData.Trip.Id,
            SeatId = seedData.Seat1.Id,
            BoardingStopOrder = 1,
            DisembarkingStopOrder = 2,
            Status = ReservationStatus.Pending
        };
        booking.SeatReservations.Add(reservation);

        context.Bookings.Add(booking);
        context.SaveChanges();
        return booking;
    }

    /// <summary>
    /// Tự đóng vai máy chủ VNPAY: ký một bộ tham số IPN theo đúng thuật toán VnPayService.ValidateSignature
    /// mong đợi (ASCII sort + UrlEncode + HMAC-SHA512), dùng để giả lập webhook gửi về.
    /// </summary>
    private static Dictionary<string, string> BuildSignedIpnQuery(IDictionary<string, string> fields, string hashSecret)
    {
        var sorted = new SortedDictionary<string, string>(fields, StringComparer.Ordinal);
        var raw = new StringBuilder();
        foreach (var (key, value) in sorted)
        {
            raw.Append(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value) + "&");
        }
        var rawData = raw.ToString().TrimEnd('&');

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret));
        var hash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData))).Replace("-", string.Empty).ToLower();

        var result = new Dictionary<string, string>(fields) { ["vnp_SecureHash"] = hash };
        return result;
    }

    private static Dictionary<string, string> BuildBaseFields(string bookingCode, long amount, string responseCode) => new()
    {
        ["vnp_TmnCode"] = TmnCode,
        ["vnp_Amount"] = amount.ToString(),
        ["vnp_TxnRef"] = bookingCode,
        ["vnp_ResponseCode"] = responseCode,
        ["vnp_TransactionNo"] = "14000111",
        ["vnp_PayDate"] = "20261002153045"
    };

    [Fact]
    public async Task Handle_WhenPaymentSucceeds_ShouldConfirmBookingAndSaveTransaction()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var booking = SeedPendingBooking(context, 150000m);
        var vnPayService = CreateVnPayService();
        var handler = new ConfirmVnPayIpnCommandHandler(context, vnPayService);

        var fields = BuildBaseFields(booking.BookingCode, 15000000, "00");
        var query = BuildSignedIpnQuery(fields, HashSecret);

        var result = await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None);

        result.RspCode.Should().Be("00");

        var updated = context.Bookings.Single(b => b.Id == booking.Id);
        updated.Status.Should().Be(BookingStatus.Confirmed);
        updated.PaymentStatus.Should().Be(PaymentStatus.Success);

        var tickets = context.Tickets.Where(t => t.BookingId == booking.Id).ToList();
        tickets.Should().NotBeEmpty();
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Valid);

        context.PaymentTransactions.Should().ContainSingle(t => t.BookingId == booking.Id);
    }

    [Fact]
    public async Task Handle_WhenTmnCodeDoesNotMatch_ShouldRejectAndNotMutateBooking()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var booking = SeedPendingBooking(context, 150000m);
        var vnPayService = CreateVnPayService();
        var handler = new ConfirmVnPayIpnCommandHandler(context, vnPayService);

        var fields = BuildBaseFields(booking.BookingCode, 15000000, "00");
        fields["vnp_TmnCode"] = "WRONG_MERCHANT";
        var query = BuildSignedIpnQuery(fields, HashSecret);

        var result = await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None);

        result.RspCode.Should().Be("99");
        context.Bookings.Single(b => b.Id == booking.Id).Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task Handle_WhenSignatureIsTampered_ShouldReject()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var booking = SeedPendingBooking(context, 150000m);
        var vnPayService = CreateVnPayService();
        var handler = new ConfirmVnPayIpnCommandHandler(context, vnPayService);

        var fields = BuildBaseFields(booking.BookingCode, 15000000, "00");
        var query = BuildSignedIpnQuery(fields, HashSecret);
        query["vnp_ResponseCode"] = "24"; // sửa sau khi đã ký -> chữ ký không còn khớp

        var result = await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None);

        result.RspCode.Should().Be("97");
    }

    [Fact]
    public async Task Handle_WhenAmountDoesNotMatchBooking_ShouldReject()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var booking = SeedPendingBooking(context, 150000m);
        var vnPayService = CreateVnPayService();
        var handler = new ConfirmVnPayIpnCommandHandler(context, vnPayService);

        var fields = BuildBaseFields(booking.BookingCode, 1, "00"); // số tiền sai hoàn toàn so với booking.TotalAmount
        var query = BuildSignedIpnQuery(fields, HashSecret);

        var result = await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None);

        result.RspCode.Should().Be("04");
    }

    [Fact]
    public async Task Handle_WhenBookingAlreadyConfirmed_ShouldRejectReplay()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var booking = SeedPendingBooking(context, 150000m);
        booking.Status = BookingStatus.Confirmed;
        context.SaveChanges();

        var vnPayService = CreateVnPayService();
        var handler = new ConfirmVnPayIpnCommandHandler(context, vnPayService);

        var fields = BuildBaseFields(booking.BookingCode, 15000000, "00");
        var query = BuildSignedIpnQuery(fields, HashSecret);

        var result = await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None);

        result.RspCode.Should().Be("02");
    }
}
