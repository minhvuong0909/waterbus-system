using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using System.Collections.Concurrent;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Validators.SightseeingAudio;

namespace WaterbusSystem.UnitTests.Services.AudioStorage.Fakes;

/// <summary>
/// Fake implementation của IAudioStorageService phục vụ kiểm thử đơn vị và phát triển offline.
/// Giữ dữ liệu trong bộ nhớ; URI trả về chỉ mô phỏng định danh ổn định,
/// không phải asset bền vững thật và không được lưu vào AudioGuide production.
/// </summary>
public class FakeAudioStorageService : IAudioStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _storage = new();
    private readonly StoreAudioRequestValidator _validator = new();
    private readonly string _baseDurablePrefix;
    private readonly bool _simulateFailure;

    public FakeAudioStorageService(
        string baseDurablePrefix = "fake-audio://audio-guides-private",
        bool simulateFailure = false)
    {
        _baseDurablePrefix = baseDurablePrefix.TrimEnd('/');
        _simulateFailure = simulateFailure;
    }

    public Task<AudioStorageResult> StoreAudioAsync(
        StoreAudioRequest command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_simulateFailure)
        {
            throw new AudioStorageException("Mô phỏng lỗi kết nối máy chủ lưu trữ âm thanh.");
        }

        var validationResult = _validator.Validate(command);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Kiểm tra toàn bộ điều kiện trước khi thay đổi trạng thái fake.
        var durableUri = $"{_baseDurablePrefix}/{command.AssetKey}";

        if (durableUri.Length > 500)
        {
            throw new AudioStorageException($"DurableAssetUri vượt quá 500 ký tự cho phép ({durableUri.Length} ký tự).");
        }

        var dataCopy = (byte[])command.AudioData.Clone();
        var stored = _storage.GetOrAdd(command.AssetKey, dataCopy);
        if (!stored.AsSpan().SequenceEqual(dataCopy))
            throw new AudioStorageException("AssetKey đã tồn tại với nội dung khác; cần khóa mới cho phiên bản audio mới.");

        var result = new AudioStorageResult(
            DurableAssetUri: durableUri,
            ContentType: command.ContentType,
            ByteLength: command.AudioData.Length,
            StoredAt: DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }

    public Task<bool> ExistsAsync(
        string assetIdentifier,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_simulateFailure)
        {
            throw new AudioStorageException("Mô phỏng lỗi kiểm tra tồn tại tệp lưu trữ.");
        }

        if (string.IsNullOrWhiteSpace(assetIdentifier))
        {
            return Task.FromResult(false);
        }

        // Kiểm tra theo key hoặc theo full URI
        var ownPrefix = _baseDurablePrefix + "/";
        if (assetIdentifier.Contains("://", StringComparison.Ordinal)
            && !assetIdentifier.StartsWith(ownPrefix, StringComparison.Ordinal))
            return Task.FromResult(false);

        var key = assetIdentifier.StartsWith(ownPrefix, StringComparison.Ordinal)
            ? assetIdentifier[ownPrefix.Length..]
            : assetIdentifier;

        return Task.FromResult(_storage.ContainsKey(key));
    }

    public Task<StoredAudio?> ReadAudioAsync(
        string assetKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_simulateFailure)
        {
            throw new AudioStorageException("Mô phỏng lỗi đọc tệp lưu trữ.");
        }

        return Task.FromResult(_storage.TryGetValue(assetKey, out var bytes)
            ? new StoredAudio((byte[])bytes.Clone(), "audio/mpeg")
            : null);
    }

    /// <summary>
    /// Phương thức hỗ trợ kiểm thử: lấy dữ liệu byte thô đã lưu trong fake storage.
    /// </summary>
    public byte[]? GetStoredBytes(string assetKey)
    {
        return _storage.TryGetValue(assetKey, out var bytes) ? (byte[])bytes.Clone() : null;
    }
}
