namespace WaterbusSystem.Application.Common.Models;

/// <summary>
/// Kết quả lưu trữ tệp âm thanh trong kho lưu trữ bền vững.
/// Adapter production trả về DurableAssetUri an toàn để lưu vào AudioGuide.AudioAssetUri.
/// Fake trong unit test chỉ mô phỏng URI này, không chứng minh đã upload bền vững.
/// </summary>
/// <param name="DurableAssetUri">
/// URI hoặc định danh bền vững (tối đa 500 ký tự).
/// Tuyệt đối KHÔNG phải SAS URL có thời hạn hết hạn, và KHÔNG phải đường dẫn file tạm thời.
/// </param>
/// <param name="ContentType">MIME type ("audio/mpeg").</param>
/// <param name="ByteLength">Kích thước file lưu trữ (byte).</param>
/// <param name="StoredAt">Thời điểm lưu trữ.</param>
public record AudioStorageResult(
    string DurableAssetUri,
    string ContentType,
    long ByteLength,
    DateTimeOffset StoredAt);
