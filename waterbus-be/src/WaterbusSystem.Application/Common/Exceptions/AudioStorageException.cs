namespace WaterbusSystem.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ nảy sinh khi quá trình lưu trữ âm thanh gặp sự cố
/// (lỗi kết nối storage, khóa tệp không hợp lệ, hoặc dữ liệu lưu bị hỏng).
/// Đảm bảo không ghi đè hoặc làm lộ storage key/connection string trong message.
/// </summary>
public class AudioStorageException : Exception
{
    public AudioStorageException(string message) : base(message)
    {
    }

    public AudioStorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
