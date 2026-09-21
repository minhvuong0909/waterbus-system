using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.WebApi.Controllers;

public record CreateVnPayUrlRequest(Guid BookingId);

/// <summary>
/// Quản lý thanh toán qua cổng VNPAY có chữ ký số HMAC-SHA512
/// </summary>
public class PaymentsController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IVnPayService _vnPayService;

    public PaymentsController(IApplicationDbContext context, IVnPayService vnPayService)
    {
        _context = context;
        _vnPayService = vnPayService;
    }

    /// <summary>
    /// Sinh URL chuyển hướng khách hàng sang cổng thanh toán VNPAY
    /// </summary>
    [HttpPost("create-vnpay-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateVnPayUrl([FromBody] CreateVnPayUrlRequest request)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId && !b.IsDeleted);

        if (booking == null)
        {
            return NotFound(new { message = "Không tìm thấy đơn đặt vé tương ứng." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var orderInfo = $"Thanh toan ve buyt duong song {booking.BookingCode}";

        var paymentUrl = _vnPayService.CreatePaymentUrl(booking.BookingCode, booking.TotalAmount, orderInfo, ipAddress);

        return Ok(new { paymentUrl, bookingCode = booking.BookingCode });
    }

    /// <summary>
    /// Webhook IPN nhận kết quả thanh toán ngầm từ VNPAY (Xác thực HMAC-SHA512 an toàn)
    /// </summary>
    [HttpGet("vnpay-ipn")]
    public async Task<IActionResult> VnPayIpn()
    {
        // 1. Kiểm tra chữ ký số HMAC-SHA512 bằng FixedTimeEquals
        var isValidSignature = _vnPayService.ValidateSignature(ToQueryDictionary(Request.Query));
        if (!isValidSignature)
        {
            return Ok(new { RspCode = "97", Message = "Invalid Signature" });
        }

        var bookingCode = Request.Query["vnp_TxnRef"].ToString();
        var vnpResponseCode = Request.Query["vnp_ResponseCode"].ToString();
        var vnpTransactionNo = Request.Query["vnp_TransactionNo"].ToString();
        var vnpAmount = long.Parse(Request.Query["vnp_Amount"].ToString());

        var booking = await _context.Bookings
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.BookingCode == bookingCode && !b.IsDeleted);

        if (booking == null)
        {
            return Ok(new { RspCode = "01", Message = "Order not found" });
        }

        // Kiểm tra số tiền khớp với đơn đặt
        if ((long)(booking.TotalAmount * 100) != vnpAmount)
        {
            return Ok(new { RspCode = "04", Message = "Invalid Amount" });
        }

        // Kiểm tra đơn đã cập nhật trước đó chưa (tránh Replay Attack)
        if (booking.Status == BookingStatus.Confirmed)
        {
            return Ok(new { RspCode = "02", Message = "Order already confirmed" });
        }

        if (vnpResponseCode == "00")
        {
            // Thanh toán thành công: Cập nhật booking và vé thành Valid
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

        await _context.SaveChangesAsync();
        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }

    private static IReadOnlyDictionary<string, string> ToQueryDictionary(IQueryCollection query) =>
        query.ToDictionary(q => q.Key, q => q.Value.ToString());

    /// <summary>
    /// Xử lý callback khi trình duyệt khách hàng chuyển hướng trở lại sau thanh toán
    /// </summary>
    [HttpGet("vnpay-return")]
    public async Task<IActionResult> VnPayReturn()
    {
        var isValid = _vnPayService.ValidateSignature(ToQueryDictionary(Request.Query));
        var responseCode = Request.Query["vnp_ResponseCode"].ToString();
        var bookingCode = Request.Query["vnp_TxnRef"].ToString();

        return Ok(new
        {
            success = isValid && responseCode == "00",
            bookingCode,
            responseCode,
            message = responseCode == "00" ? "Thanh toán thành công" : "Giao dịch không thành công hoặc bị hủy"
        });
    }
}
