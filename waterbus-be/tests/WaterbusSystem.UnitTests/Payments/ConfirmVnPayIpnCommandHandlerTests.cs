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

        // HttpClient không dùng cho luồng IPN (chỉ verify chữ ký) nhưng vẫn phải cấp để khớp constructor.
        return new VnPayService(configuration, new HttpClient(new NeverCalledHandler()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<VnPayService>.Instance);
    }

    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Test IPN không được phát sinh request ra ngoài.");
    }

    /// <summary>
    /// Tạo một Order đang chờ thanh toán với đúng 1 Booking + 1 SeatReservation, giống hệt những gì
    /// CreateBookingCommand tạo ra. QUAN TRỌNG: không tạo Ticket sẵn, vì BR-COM-08 quy định Ticket
    /// chỉ được phát hành sau khi backend xác minh thanh toán thành công.
    /// </summary>
    private static Booking SeedPendingBooking(Infrastructure.Persistence.ApplicationDbContext context, decimal totalAmount)
    {
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = TestDbContextFactory.SeedBooking(context, seed, fare: totalAmount);
        booking.PaymentStatus = PaymentStatus.Pending;

        var reservation = new SeatReservation
        {
            Booking = booking, BookingId = booking.Id, TripId = seed.Trip.Id, SeatId = seed.Seat1.Id,
            BoardingStopOrder = 1, DisembarkingStopOrder = 3, PassengerName = booking.CustomerName,
            QuotedFare = totalAmount, SeatCodeAtSale = seed.Seat1.SeatCode, SeatClassAtSale = seed.StandardClass.Name,
            Status = ReservationStatus.Pending
        };
        context.SeatReservations.Add(reservation);

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
    public async Task Handle_WhenPaymentSucceeds_ShouldConfirmOrderIssueTicketsAndRecordPayment()
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

        // BR-PAY-04: Booking phải có số tiền thực tế được phân bổ rõ ràng.
        updated.PaidAllocation.Should().Be(150000m);

        var order = context.PurchaseOrders.Single(o => o.Id == updated.OrderId);
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().NotBeNull();

        // BR-COM-08: Ticket chỉ được phát hành SAU khi backend xác minh thanh toán thành công.
        var tickets = context.Tickets.Where(t => t.BookingId == booking.Id).ToList();
        tickets.Should().ContainSingle();
        var ticket = tickets[0];
        ticket.Status.Should().Be(TicketStatus.Valid);
        ticket.BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
        ticket.PaidFareSnapshot.Should().Be(150000m);
        ticket.FareTripTypeSnapshot.Should().Be(nameof(TripType.Commuter));
        ticket.IssuedAt.Should().NotBeNull();

        // BR-PAY-03: ghi lại một FinancialTransaction Payment thành công cho Order này.
        var payment = context.FinancialTransactions.Single();
        payment.TransactionType.Should().Be(FinancialTransactionType.Payment);
        payment.Status.Should().Be(FinancialTransactionStatus.Succeeded);
        payment.OrderId.Should().Be(order.Id);
        payment.Amount.Should().Be(150000m);
        payment.IdempotencyKey.Should().Be($"{booking.BookingCode}_14000111");
        payment.RefundBookingId.Should().BeNull();
        payment.OriginalPaymentId.Should().BeNull();
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
        context.Bookings.Single(b => b.Id == booking.Id).Status.Should().Be(BookingStatus.PendingPayment);
        context.Tickets.Should().BeEmpty();
        context.FinancialTransactions.Should().BeEmpty();
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
    public async Task Handle_WhenAmountDoesNotMatchOrder_ShouldReject()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var booking = SeedPendingBooking(context, 150000m);
        var vnPayService = CreateVnPayService();
        var handler = new ConfirmVnPayIpnCommandHandler(context, vnPayService);

        var fields = BuildBaseFields(booking.BookingCode, 1, "00"); // số tiền sai hoàn toàn so với tổng Order
        var query = BuildSignedIpnQuery(fields, HashSecret);

        var result = await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None);

        result.RspCode.Should().Be("04");
    }

    [Fact]
    public async Task Handle_WhenSameIpnIsDeliveredTwice_ShouldNotDuplicatePaymentOrTicket()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var booking = SeedPendingBooking(context, 150000m);
        var vnPayService = CreateVnPayService();
        var handler = new ConfirmVnPayIpnCommandHandler(context, vnPayService);

        var fields = BuildBaseFields(booking.BookingCode, 15000000, "00");
        var query = BuildSignedIpnQuery(fields, HashSecret);

        (await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None)).RspCode.Should().Be("00");

        // BR-PAY-01: gateway retry cùng IdempotencyKey không được ghi nhận lần hai.
        var second = await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None);

        second.RspCode.Should().Be("02");
        context.FinancialTransactions.Should().ContainSingle();
        context.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenOrderAlreadyConfirmed_ShouldRejectReplay()
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

    [Fact]
    public async Task Handle_WhenPaymentFails_ShouldRecordFailedTransactionAndIssueNoTicket()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var booking = SeedPendingBooking(context, 150000m);
        var vnPayService = CreateVnPayService();
        var handler = new ConfirmVnPayIpnCommandHandler(context, vnPayService);

        var fields = BuildBaseFields(booking.BookingCode, 15000000, "24"); // 24 = giao dịch bị hủy
        var query = BuildSignedIpnQuery(fields, HashSecret);

        var result = await handler.Handle(new ConfirmVnPayIpnCommand(query), CancellationToken.None);

        result.RspCode.Should().Be("00"); // đã xử lý xong, chỉ là thanh toán không thành công

        var updated = context.Bookings.Single(b => b.Id == booking.Id);
        updated.Status.Should().Be(BookingStatus.Cancelled);
        updated.PaymentStatus.Should().Be(PaymentStatus.Failed);

        // BR-PAY-05: thanh toán không thành công không được phát hành Ticket.
        context.Tickets.Should().BeEmpty();
        context.SeatReservations.Single(r => r.BookingId == booking.Id)
            .Status.Should().Be(ReservationStatus.Cancelled);

        // BR-PAY-03: lần thử thất bại vẫn phải được ghi nhận để đối soát.
        var payment = context.FinancialTransactions.Single();
        payment.Status.Should().Be(FinancialTransactionStatus.Failed);
        payment.FailureReason.Should().NotBeNullOrEmpty();
    }
}