using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Tickets;
using WaterbusSystem.Application.DTOs.Responses.Tickets;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Tickets.Services;

/// <summary>
/// Tra cứu vé bằng mã đặt chỗ + email người mua, xác thực bằng OTP gửi về chính email đó.
/// Bảo mật: phản hồi xin OTP cố định (không lộ mã nào tồn tại); OTP chỉ lưu dạng HMAC ở Redis với TTL,
/// giới hạn số lần thử/gửi, dùng một lần; mọi thất bại khi xác minh cùng một thông báo; không log email/mã/OTP.
/// </summary>
public sealed class TicketLookupService(
    ITicketLookupRepository repository,
    ITicketLookupOtpStore store,
    ITicketLookupHasher hasher,
    ITicketLookupOtpMailer mailer,
    IBookingQrSigner qrSigner,
    IOptions<TicketLookupOptions> options,
    ILogger<TicketLookupService> logger) : ITicketLookupService
{
    private const string VerifyFailedMessage = "Mã xác thực không đúng hoặc đã hết hạn. Vui lòng yêu cầu mã mới.";
    private readonly TicketLookupOptions _options = options.Value;

    public async Task<TicketLookupOtpResponse> RequestOtpAsync(RequestTicketLookupOtpRequest request, CancellationToken ct = default)
    {
        var (email, bookingCode) = Normalize(request.Email, request.BookingCode);
        var response = new TicketLookupOtpResponse(_options.OtpTtlMinutes * 60, _options.ResendCooldownSeconds);

        try
        {
            var match = await repository.FindMatchAsync(bookingCode, email, ct);
            if (match is null) return response; // không khớp: im lặng, cùng phản hồi

            var identity = IdentityOf(email, bookingCode);
            if (!await store.TryReserveSendAsync(identity, TimeSpan.FromSeconds(_options.ResendCooldownSeconds), _options.MaxSendsPerHour, ct))
                return response; // đang chờ hoặc quá số lần gửi: không gửi, cùng phản hồi

            var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var ttl = TimeSpan.FromMinutes(_options.OtpTtlMinutes);
            await store.SaveAsync(identity, OtpHashOf(identity, otp), ttl, ct);
            mailer.Enqueue(email, match.PurchaserName, otp, ttl);
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            // Redis không sẵn sàng: không gửi OTP không kiểm soát được. Vẫn trả cùng phản hồi để không lộ trạng thái.
            logger.LogError(ex, "Ticket lookup OTP store unavailable.");
        }
        return response;
    }

    public async Task<TicketLookupResponse> VerifyOtpAsync(VerifyTicketLookupOtpRequest request, CancellationToken ct = default)
    {
        var (email, bookingCode) = Normalize(request.Email, request.BookingCode);
        var identity = IdentityOf(email, bookingCode);

        OtpAttempt attempt;
        try
        {
            attempt = await store.RegisterAttemptAsync(identity, _options.MaxVerifyAttempts, ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Ticket lookup OTP store unavailable.");
            throw new ForbiddenAccessException(VerifyFailedMessage);
        }
        if (attempt.OtpHash is null) throw new ForbiddenAccessException(VerifyFailedMessage);

        if (!hasher.Equals(OtpHashOf(identity, request.Otp), attempt.OtpHash))
        {
            // Hết lượt thử thì huỷ OTP ngay, không chờ lần thử kế tiếp.
            if (attempt.Attempts >= _options.MaxVerifyAttempts) await store.RemoveAsync(identity, ct);
            throw new ForbiddenAccessException(VerifyFailedMessage);
        }

        // OTP đúng: dùng một lần. Hai yêu cầu song song chỉ một bên xoá được.
        if (!await store.RemoveAsync(identity, ct)) throw new ForbiddenAccessException(VerifyFailedMessage);

        // Kiểm tra lại khớp ở thời điểm đọc (đơn có thể đã đổi/xoá từ lúc gửi OTP).
        var match = await repository.FindMatchAsync(bookingCode, email, ct) ?? throw new ForbiddenAccessException(VerifyFailedMessage);
        var bookings = await repository.GetBookingsAsync(match.OrderId, ct);
        return new TicketLookupResponse(bookings.Select(ToDto).ToList());
    }

    private TicketLookupBookingDto ToDto(TicketLookupBookingData b)
    {
        // Cùng điều kiện cấp QR của BookingQrService: Booking đã thanh toán và đã phát hành vé.
        BookingQrCredential? qr = null;
        if (b.Status is BookingStatus.Confirmed or BookingStatus.Completed && b.Tickets.Count > 0
            && (b.QrIssuedAt ?? b.ConfirmedAt ?? b.FirstTicketIssuedAt) is { } issuedAt)
        {
            qr = qrSigner.Sign(b.PublicBookingId, b.QrCredentialVersion, b.TripId, issuedAt);
        }
        return new TicketLookupBookingDto(b.BookingCode, b.RouteName, b.DepartureTime, b.ArrivalTime, b.TripStatus,
            b.BoardingStation, b.DisembarkingStation, b.Status.ToString(), b.Tickets, qr);
    }

    private static (string Email, string BookingCode) Normalize(string email, string bookingCode) =>
        (email.Trim().ToLowerInvariant(), bookingCode.Trim().ToUpperInvariant());

    private string IdentityOf(string email, string bookingCode) => hasher.Hash("lookup-id", $"{email}|{bookingCode}");

    // Gắn OTP với định danh: OTP của cặp này không dùng được cho cặp khác dù trùng giá trị.
    private string OtpHashOf(string identity, string otp) => hasher.Hash("lookup-otp:" + identity, otp);
}
