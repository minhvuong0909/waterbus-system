using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ lưu trữ tệp âm thanh (MP3) cho kịch bản thuyết minh POI.
/// Application chỉ phụ thuộc interface này; Infrastructure triển khai qua
/// Azure Blob Storage (khi được cấp quyền) hoặc Fake adapter (cho test/offline).
/// </summary>
public interface IAudioStorageService
{
    /// <summary>
    /// Lưu trữ tệp âm thanh MP3 và trả về định danh / URI bền vững dùng để lưu vào AudioGuide.AudioAssetUri.
    /// </summary>
    /// <param name="command">Dữ liệu âm thanh và metadata cần lưu trữ.</param>
    /// <param name="cancellationToken">Token hủy tác vụ.</param>
    /// <returns>Đối tượng <see cref="AudioStorageResult"/> chứa DurableAssetUri bền vững.</returns>
    /// <exception cref="Exceptions.AudioStorageException">Khi quá trình lưu trữ gặp sự cố.</exception>
    /// <exception cref="OperationCanceledException">Khi tác vụ bị hủy thông qua token.</exception>
    Task<AudioStorageResult> StoreAudioAsync(
        StoreAudioRequest command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra xem tệp âm thanh với định danh bền vững đã tồn tại trong kho lưu trữ hay chưa.
    /// </summary>
    /// <param name="assetIdentifier">Định danh tệp cần kiểm tra.</param>
    /// <param name="cancellationToken">Token hủy tác vụ.</param>
    /// <returns><c>true</c> nếu tệp tồn tại; ngược lại <c>false</c>.</returns>
    Task<bool> ExistsAsync(
        string assetIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc lại tệp âm thanh đã lưu theo AssetKey để phát qua backend (Admin nghe thử).
    /// </summary>
    /// <param name="assetKey">Khóa tương đối của tệp, ví dụ <c>audio-assets/tts/20261023/abc.mp3</c>.</param>
    /// <param name="cancellationToken">Token hủy tác vụ.</param>
    /// <returns>Nội dung tệp; <c>null</c> nếu khóa không hợp lệ hoặc tệp không tồn tại.</returns>
    /// <exception cref="Exceptions.AudioStorageException">Khi kho lưu trữ gặp sự cố.</exception>
    Task<StoredAudio?> ReadAudioAsync(
        string assetKey,
        CancellationToken cancellationToken = default);
}
