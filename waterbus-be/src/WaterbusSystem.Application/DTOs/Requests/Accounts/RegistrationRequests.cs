namespace WaterbusSystem.Application.DTOs.Requests.Accounts;

/// <summary>Khách (Guest) đăng ký tài khoản khách hàng (role Passenger). Role luôn do hệ thống gán, không đọc từ request.</summary>
public record RegisterRequest(string Email, string Password, string FullName, string? PhoneNumber);

/// <summary>Xác minh email bằng liên kết gửi về hộp thư (token của ASP.NET Identity, mã hoá Base64Url).</summary>
public record ConfirmEmailRequest(Guid UserId, string Token);

/// <summary>Xin gửi lại email xác minh. Luôn trả cùng một phản hồi dù email có tồn tại hay không.</summary>
public record ResendVerificationRequest(string Email);

/// <summary>Cập nhật hồ sơ của chính tài khoản đang đăng nhập. Email không đổi qua đây.</summary>
public record UpdateProfileRequest(string FullName, string? PhoneNumber);
