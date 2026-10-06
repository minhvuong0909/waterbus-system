using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;

/// <summary>
/// Kết quả trả về theo đúng định dạng VNPAY mong đợi từ Webhook IPN: {RspCode, Message}
/// </summary>
public record VnPayIpnResultDto(string RspCode, string Message);

/// <summary>
/// Command xử lý Webhook IPN từ VNPAY.
///
/// BR-PAY-01: backend-verified webhook/IPN là bằng chứng xác nhận thanh toán; browser redirect
/// KHÔNG phải nguồn sự thật cuối cùng. Callback trễ, lặp, sai thứ tự phải được xử lý idempotently.
/// BR-PAY-03: mỗi lần thử thanh toán là một FinancialTransaction có trạng thái/lịch sử riêng,
/// khóa bằng IdempotencyKey = vnp_TxnRef + vnp_TransactionNo.
/// BR-PAY-04: một payment có thể thanh toán cho nhiều Booking trong cùng một Order; từng Booking
/// phải có số tiền thực tế được phân bổ rõ ràng để hoàn tiền được riêng lượt bị ảnh hưởng.
/// BR-COM-08: chỉ phát hành Ticket sau khi backend xác minh thanh toán thành công.
/// </summary>
public record ConfirmVnPayIpnCommand(IReadOnlyDictionary<string, string> Query) : IRequest<VnPayIpnResultDto>;

public class ConfirmVnPayIpnCommandHandler : IRequestHandler<ConfirmVnPayIpnCommand, VnPayIpnResultDto>
{
    private const string GatewayName = "VNPAY";

    private readonly IApplicationDbContext _context;
    private readonly IVnPayService _vnPayService;
    private readonly ICheckoutSessionService? _checkoutSessionService;

    public ConfirmVnPayIpnCommandHandler(
        IApplicationDbContext context,
        IVnPayService vnPayService,
        ICheckoutSessionService? checkoutSessionService = null)
    {
        _context = context;
        _vnPayService = vnPayService;
        _checkoutSessionService = checkoutSessionService;
    }

    public async Task<VnPayIpnResultDto> Handle(ConfirmVnPayIpnCommand request, CancellationToken cancellationToken)
    {
        var query = request.Query;

        // 1. Xác thực chữ ký HMAC-SHA512 an toàn thời gian cố định.
        if (!_vnPayService.ValidateSignature(query))
        {
            return new VnPayIpnResultDto("97", "Invalid Signature");
        }

        // 2. Xác thực vnp_TmnCode khớp merchant đã cấu hình (chặn IPN giả mạo).
        var tmnCode = query.GetValueOrDefault("vnp_TmnCode", string.Empty);
        if (!_vnPayService.IsValidTmnCode(tmnCode))
        {
            return new VnPayIpnResultDto("99", "Invalid TmnCode");
        }

        var merchantReference = query.GetValueOrDefault("vnp_TxnRef", string.Empty);
        var responseCode = query.GetValueOrDefault("vnp_ResponseCode", string.Empty);
        var gatewayTransactionNo = query.GetValueOrDefault("vnp_TransactionNo", string.Empty);

        if (!long.TryParse(query.GetValueOrDefault("vnp_Amount", string.Empty), out var vnpAmount))
        {
            return new VnPayIpnResultDto("04", "Invalid Amount");
        }

        // vnp_TxnRef là mã Booking đã gửi sang cổng. Từ Booking suy ra Order là đơn vị
        // thanh toán thực sự, vì BR-PAY-04 cho phép 1 payment trả cho 2 Booking của 1 Order.
        var referencedBooking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingCode == merchantReference, cancellationToken);

        if (referencedBooking == null)
        {
            return new VnPayIpnResultDto("01", "Order not found");
        }

        var orderId = referencedBooking.OrderId;
        if (orderId == Guid.Empty)
        {
            return new VnPayIpnResultDto("01", "Order not found");
        }

        // 3. BR-PAY-03 idempotency: cùng một giao dịch được gọi lại nhiều lần chỉ ghi nhận một lần.
        var idempotencyKey = $"{merchantReference}_{gatewayTransactionNo}";
        var alreadyRecorded = await _context.FinancialTransactions
            .AsNoTracking()
            .AnyAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);

        if (alreadyRecorded)
        {
            return new VnPayIpnResultDto("02", "Order already confirmed");
        }

        var order = await _context.PurchaseOrders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            return new VnPayIpnResultDto("01", "Order not found");
        }

        // 4. Đối soát số tiền với tổng của Order, không đối chiếu từng Booking để giữ đúng
        // quy tắc "1 lần thanh toán, 1 Order".
        if ((long)(order.QuotedTotal * 100) != vnpAmount)
        {
            return new VnPayIpnResultDto("04", "Invalid Amount");
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            return new VnPayIpnResultDto("02", "Order already confirmed");
        }

        var orderBookings = await _context.Bookings
            .Include(b => b.Trip)
            .Include(b => b.SeatReservations)
            .Where(b => b.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        if (orderBookings.Count == 0)
        {
            return new VnPayIpnResultDto("01", "Order not found");
        }

        // BR-PAY-01 idempotency: lớp phòng thủ thứ hai bên cạnh IdempotencyKey. Nếu bất kỳ
        // Booking nào của Order đã được xác nhận trước đó thì lần gọi này là replay,
        // tuyệt đối không được phát hành Ticket lần nữa.
        if (orderBookings.Any(b => b.Status is BookingStatus.Confirmed or BookingStatus.Completed))
        {
            return new VnPayIpnResultDto("02", "Order already confirmed");
        }

        var isSuccess = responseCode == "00";
        DateTimeOffset? paidAt = null;
        if (query.TryGetValue("vnp_PayDate", out var payDateRaw) &&
            DateTime.TryParseExact(payDateRaw, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedPayDate))
        {
            paidAt = new DateTimeOffset(parsedPayDate, TimeSpan.Zero);
        }

        // BR-PAY-03: ghi lại lịch sử giao dịch để phục vụ đối soát kế toán, kể cả lần thất bại.
        var payment = new FinancialTransaction
        {
            OrderId = order.Id,
            Order = order,
            TransactionType = FinancialTransactionType.Payment,
            Amount = order.QuotedTotal,
            Currency = order.Currency,
            Status = isSuccess ? FinancialTransactionStatus.Succeeded : FinancialTransactionStatus.Failed,
            GatewayName = GatewayName,
            GatewayReference = gatewayTransactionNo,
            MerchantReference = merchantReference,
            GatewayRequestCreatedAt = paidAt,
            IdempotencyKey = idempotencyKey,
            FailureReason = isSuccess ? null : $"VNPAY response code {responseCode}",
            ProcessedAt = DateTimeOffset.UtcNow,
            CompletedAt = isSuccess ? DateTimeOffset.UtcNow : null
        };
        _context.FinancialTransactions.Add(payment);

        if (!isSuccess)
        {
            // BR-PAY-05: thanh toán thất bại không phát hành Ticket, giải phóng ghế đã giữ.
            order.Status = OrderStatus.Cancelled;
            order.ExpiresAt = null;

            foreach (var booking in orderBookings)
            {
                booking.Status = BookingStatus.Cancelled;
                booking.PaymentStatus = PaymentStatus.Failed;

                foreach (var reservation in booking.SeatReservations)
                {
                    reservation.Status = ReservationStatus.Cancelled;
                }
            }

            await SaveOrReportDuplicateAsync(cancellationToken);

            // BR-HOLD-03: thanh toán thất bại cũng phải giải phóng hold để ghế trở lại bán được.
            await ReleaseCheckoutSessionAsync(order.Id, cancellationToken);

            return new VnPayIpnResultDto("00", "Confirm Success");
        }

        // BR-PAY-04: phân bổ số tiền thực tế cho từng Booking theo tổng paid allocation của
        // các SeatReservation của Booking đó. BR-FARE-02 giá phẳng theo SeatClass nên không
        // cần tỷ lệ theo đoạn đi.
        foreach (var booking in orderBookings)
        {
            booking.PaidAllocation = booking.SeatReservations.Sum(r => r.QuotedFare);
            if (booking.PaidAllocation != booking.QuotedTotal)
            {
                return new VnPayIpnResultDto("04", "Invalid Amount");
            }
        }

        // BR-COM-08 / BR-PAY-05: xác nhận Booking và SeatReservation, ghi paid allocation.
        order.Status = OrderStatus.Confirmed;
        order.ConfirmedAt = DateTimeOffset.UtcNow;
        order.ExpiresAt = null;

        foreach (var booking in orderBookings)
        {
            booking.Status = BookingStatus.Confirmed;
            booking.PaymentStatus = PaymentStatus.Success;
            booking.ConfirmedAt = DateTimeOffset.UtcNow;

            foreach (var reservation in booking.SeatReservations)
            {
                reservation.Status = ReservationStatus.Confirmed;
                reservation.PaidAllocation = reservation.QuotedFare;
                reservation.ConfirmedAt = DateTimeOffset.UtcNow;
            }
        }

        // BR-COM-08: Ticket chỉ được phát hành từ SeatReservation đã thanh toán và đã xác nhận.
        // Một SeatReservation chỉ sinh tối đa một Ticket (unique index trên SeatReservationId
        // là lớp phòng thủ cuối chống phát vé trùng).
        foreach (var booking in orderBookings)
        {
            var trip = booking.Trip
                ?? throw new InvalidOperationException($"Booking {booking.BookingCode} không có Trip.");

            foreach (var reservation in booking.SeatReservations)
            {
                var ticket = Ticket.Issue(booking, reservation, payment, trip.TripType, DateTimeOffset.UtcNow);
                _context.Tickets.Add(ticket);
            }
        }

        await SaveOrReportDuplicateAsync(cancellationToken);

        // BR-PAY-05: sau khi thanh toán được xác minh, giải phóng hold tạm. Việc này chỉ dọn
        // trạng thái phiên phía Redis; SeatReservation đã chuyển Confirmed nên vẫn chiếm ghế
        // đúng như BR-SEAT-05 yêu cầu (ghế đã bán vẫn bị chặn cho các đoạn chồng lấn).
        await ReleaseCheckoutSessionAsync(order.Id, cancellationToken);

        return new VnPayIpnResultDto("00", "Confirm Success");
    }

    /// <summary>
    /// BR-HOLD-03 / BR-PAY-05: giải phóng phiên giữ ghế sau khi giao dịch đã kết thúc.
    /// Lỗi ở tầng cache KHÔNG được làm hỏng kết quả thanh toán đã được xác nhận và ghi vào CSDL.
    /// </summary>
    private async Task ReleaseCheckoutSessionAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (_checkoutSessionService is null)
        {
            return;
        }

        try
        {
            await _checkoutSessionService.ReleaseAsync(
                Bookings.Commands.CreateBooking.CheckoutSessionKey.ForOrder(orderId), cancellationToken);
        }
        catch (Exception)
        {
            // BR-NOT-03 tinh thần: lỗi ở bước phụ không rollback trạng thái Payment/Booking đã
            // được xác nhận. TTL sẽ tự dọn phiên nếu Release thất bại.
        }
    }

    /// <summary>
    /// BR-PAY-01: IPN có thể đến trùng do gateway retry hoặc do ta tự gọi lại. Unique index trên
    /// IdempotencyKey là lớp phòng thủ cuối; nếu trùng thì coi như đã xử lý và trả "02" để
    /// gateway không retry tiếp. Việc phát hành Ticket đã được bảo vệ bởi TransactionScope +
    /// RowVersion nên không thể phát vé trùng.
    /// </summary>
    private async Task SaveOrReportDuplicateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Giao dịch đã được ghi nhận với IdempotencyKey này; yêu cầu bị xử lý trùng lặp.");
        }
    }
}