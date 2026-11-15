namespace WaterbusSystem.Application.DTOs.Requests.Accounts;

/// <summary>Khách (Guest) đăng ký tài khoản khách hàng (role Passenger). Role luôn do hệ thống gán, không đọc từ request.</summary>
public record RegisterRequest(string Email, string FullName, string? PhoneNumber);

/// <summary>Xác minh email và đặt mật khẩu trong cùng một lần kích hoạt.</summary>
public record ConfirmEmailRequest(Guid UserId, string Token, string Password);

/// <summary>Xin gửi lại email xác minh. Luôn trả cùng một phản hồi dù email có tồn tại hay không.</summary>
public record ResendVerificationRequest(string Email);

/// <summary>Cập nhật hồ sơ của chính tài khoản đang đăng nhập. Email không đổi qua đây.</summary>
public record UpdateProfileRequest(string FullName, string? PhoneNumber);
