using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;

/// <summary>
/// Kết quả trả về theo đúng định dạng VNPAY mong đợi từ Webhook IPN: {RspCode, Message}
/// </summary>
public record VnPayIpnResultDto(string RspCode, string Message);

/// <summary>
/// Command xử lý Webhook IPN từ VNPAY: xác thực chữ ký, đối soát số tiền, chống replay,
/// cập nhật trạng thái Booking/Ticket và LƯU LẠI lịch sử giao dịch vào PaymentTransaction.
/// </summary>
public record ConfirmVnPayIpnCommand(IReadOnlyDictionary<string, string> Query) : IRequest<VnPayIpnResultDto>;

public class ConfirmVnPayIpnCommandHandler : IRequestHandler<ConfirmVnPayIpnCommand, VnPayIpnResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IVnPayService _vnPayService;

    public ConfirmVnPayIpnCommandHandler(IApplicationDbContext context, IVnPayService vnPayService)
    {
        _context = context;
        _vnPayService = vnPayService;
    }

    public async Task<VnPayIpnResultDto> Handle(ConfirmVnPayIpnCommand request, CancellationToken cancellationToken)
    {
        var query = request.Query;

        // 1. Xác thực chữ ký HMAC-SHA512 an toàn thời gian cố định
        if (!_vnPayService.ValidateSignature(query))
        {
            return new VnPayIpnResultDto("97", "Invalid Signature");
        }

        // 2. Xác thực vnp_TmnCode khớp với merchant đã cấu hình (chặn IPN giả mạo từ cấu hình khác)
        var tmnCode = query.GetValueOrDefault("vnp_TmnCode", string.Empty);
        if (!_vnPayService.IsValidTmnCode(tmnCode))
        {
            return new VnPayIpnResultDto("99", "Invalid TmnCode");
        }

        var bookingCode = query.GetValueOrDefault("vnp_TxnRef", string.Empty);
        var vnpResponseCode = query.GetValueOrDefault("vnp_ResponseCode", string.Empty);
        var vnpTransactionNo = query.GetValueOrDefault("vnp_TransactionNo", string.Empty);

        if (!long.TryParse(query.GetValueOrDefault("vnp_Amount", string.Empty), out var vnpAmount))
        {
            return new VnPayIpnResultDto("04", "Invalid Amount");
        }

        var booking = await _context.Bookings
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.BookingCode == bookingCode && !b.IsDeleted, cancellationToken);

        if (booking == null)
        {
            return new VnPayIpnResultDto("01", "Order not found");
        }

        // 3. Đối soát số tiền
        if ((long)(booking.TotalAmount * 100) != vnpAmount)
        {
            return new VnPayIpnResultDto("04", "Invalid Amount");
        }

        // 4. Chống Replay Attack: đơn đã được xử lý xong (thành công hoặc thất bại) thì không xử lý lại
        if (booking.Status is BookingStatus.Confirmed or BookingStatus.Cancelled)
        {
            return new VnPayIpnResultDto("02", "Order already confirmed");
        }

        DateTimeOffset? payDate = null;
        if (query.TryGetValue("vnp_PayDate", out var payDateRaw) &&
            DateTime.TryParseExact(payDateRaw, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedPayDate))
        {
            payDate = new DateTimeOffset(parsedPayDate, TimeSpan.Zero);
        }

        var isSuccess = vnpResponseCode == "00";

        // 5. Lưu lại lịch sử giao dịch để phục vụ đối soát kế toán (trước đây bị bỏ sót)
        _context.PaymentTransactions.Add(new PaymentTransaction
        {
            BookingId = booking.Id,
            TransactionCode = vnpTransactionNo,
            Provider = "VNPAY",
            Amount = booking.TotalAmount,
            PayDate = payDate,
            ResponseCode = vnpResponseCode,
            TransactionStatus = isSuccess ? PaymentStatus.Success : PaymentStatus.Failed,
            SecureHash = query.GetValueOrDefault("vnp_SecureHash", string.Empty)
        });

        if (isSuccess)
        {
            booking.Status = BookingStatus.Confirmed;
            booking.PaymentStatus = PaymentStatus.Success;

            foreach (var ticket in booking.Tickets)
            {
                ticket.Status = TicketStatus.Valid;
            }
        }
        else
        {
            booking.Status = BookingStatus.Cancelled;
            booking.PaymentStatus = PaymentStatus.Failed;

            foreach (var ticket in booking.Tickets)
            {
                ticket.Status = TicketStatus.Cancelled;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new VnPayIpnResultDto("00", "Confirm Success");
    }
}
