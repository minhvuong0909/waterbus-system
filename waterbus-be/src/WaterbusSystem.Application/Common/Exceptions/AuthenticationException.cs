namespace WaterbusSystem.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ đăng nhập thất bại: sai email/mật khẩu hoặc tài khoản bị khoá.
/// Luôn dùng cùng 1 message cho mọi nguyên nhân để không lộ thông tin tài khoản có tồn tại hay không.
/// Ánh xạ sang HTTP 401.
/// </summary>
public class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message) { }
}
