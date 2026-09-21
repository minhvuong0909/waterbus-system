namespace WaterbusSystem.Domain.Exceptions;

/// <summary>
/// Ngoại lệ nghiệp vụ gốc của tầng Domain
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Ngoại lệ không tìm thấy bản ghi
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string name, object key) 
        : base($"Không tìm thấy bản ghi '{name}' với khóa '{key}'.") { }
}

/// <summary>
/// Ngoại lệ xung đột dữ liệu (Concurrency/Double-booking)
/// Ánh xạ sang HTTP 409 Conflict tại WebApi
/// </summary>
public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Ngoại lệ khi ghế đã bị người khác chọn hoặc bán trước
/// </summary>
public class SeatAlreadyBookedException : ConcurrencyException
{
    public SeatAlreadyBookedException(string seatCode) 
        : base($"Ghế '{seatCode}' đã được đặt hoặc đang được giữ bởi người khác.") { }
}
