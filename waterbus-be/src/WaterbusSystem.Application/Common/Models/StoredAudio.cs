namespace WaterbusSystem.Application.Common.Models;

/// <summary>
/// Nội dung tệp âm thanh đọc lại từ kho lưu trữ (để Admin nghe thử qua backend, không lộ URL Blob private).
/// </summary>
/// <param name="Data">Dữ liệu nhị phân MP3.</param>
/// <param name="ContentType">MIME type ("audio/mpeg").</param>
public record StoredAudio(byte[] Data, string ContentType);
