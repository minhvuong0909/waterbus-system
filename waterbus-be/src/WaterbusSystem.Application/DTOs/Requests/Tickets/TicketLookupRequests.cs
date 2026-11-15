namespace WaterbusSystem.Application.DTOs.Requests.Tickets;

/// <summary>Xin mã OTP tra cứu vé: email người mua và mã đặt chỗ (BookingCode) trong email xác nhận.</summary>
public record RequestTicketLookupOtpRequest(string Email, string BookingCode);

/// <summary>Xác minh OTP (6 chữ số) đã gửi về email người mua.</summary>
public record VerifyTicketLookupOtpRequest(string Email, string BookingCode, string Otp);
