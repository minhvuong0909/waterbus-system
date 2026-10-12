namespace WaterbusSystem.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ khi người dùng đã xác thực nhưng không có quyền trên tài nguyên cụ thể
/// (vd Captain gửi vị trí cho tàu không phải của mình). Ánh xạ sang HTTP 403.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
}
