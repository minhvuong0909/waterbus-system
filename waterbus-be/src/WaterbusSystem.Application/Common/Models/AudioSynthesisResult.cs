namespace WaterbusSystem.Application.Common.Models;

/// <summary>
/// Kết quả tổng hợp giọng nói từ văn bản (TTS).
/// Chứa dữ liệu âm thanh dạng MP3 và các metadata cần thiết.
/// </summary>
/// <param name="AudioData">Mảng byte dữ liệu âm thanh (MP3).</param>
/// <param name="ContentType">MIME type của file âm thanh (mặc định "audio/mpeg").</param>
/// <param name="Duration">Thời lượng của audio clip.</param>
/// <param name="VoiceName">Tên giọng đọc đã sử dụng (e.g. "vi-VN-HoaiMyNeural").</param>
/// <param name="ByteLength">Kích thước file tính bằng byte.</param>
public record AudioSynthesisResult(
    byte[] AudioData,
    string ContentType,
    TimeSpan Duration,
    string VoiceName,
    long ByteLength);
