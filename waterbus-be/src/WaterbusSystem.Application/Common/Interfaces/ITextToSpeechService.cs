using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ tổng hợp giọng nói từ văn bản (TTS).
/// Application chỉ phụ thuộc interface này; Infrastructure triển khai qua
/// Azure AI Speech hoặc provider/stub khác.
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// Chuyển đổi văn bản tiếng Việt sang tệp âm thanh định dạng MP3.
    /// </summary>
    /// <param name="text">Văn bản cần đọc (không được rỗng hoặc chỉ chứa khoảng trắng).</param>
    /// <param name="options">Tùy chọn ghi đè giọng đọc, định dạng, timeout (nếu có).</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Đối tượng <see cref="AudioSynthesisResult"/> chứa dữ liệu MP3 và metadata.</returns>
    /// <exception cref="ArgumentException">Khi <paramref name="text"/> rỗng hoặc chỉ có khoảng trắng.</exception>
    /// <exception cref="Exceptions.TextToSpeechException">Khi provider gặp lỗi tổng hợp hoặc timeout.</exception>
    /// <exception cref="OperationCanceledException">Khi tác vụ bị hủy thông qua token.</exception>
    Task<AudioSynthesisResult> SynthesizeSpeechAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default);
}
