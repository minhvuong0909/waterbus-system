using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Infrastructure.Services.AudioStorage;

/// <summary>
/// Lưu MP3 vào Azure Blob. Container phải được cấp sẵn ở chế độ private;
/// URI trả về là định danh bền vững, không phải URL có SAS để phát trực tiếp.
/// </summary>
public sealed class AzureBlobAudioStorageService : IAudioStorageService
{
    private static readonly Regex SafeAssetKey = new(
        "^[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*\\.mp3$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Lazy<BlobContainerClient> _containerClient;
    private BlobContainerClient Container => _containerClient.Value;
    private readonly IValidator<StoreAudioRequest> _validator;
    private readonly ILogger<AzureBlobAudioStorageService> _logger;

    public AzureBlobAudioStorageService(
        BlobContainerClient container,
        IValidator<StoreAudioRequest> validator,
        ILogger<AzureBlobAudioStorageService> logger)
        : this(new Lazy<BlobContainerClient>(() => container), validator, logger)
    {
        ArgumentNullException.ThrowIfNull(container);
    }

    /// <summary>Chỉ đọc cấu hình Blob khi thực sự truy cập asset; GET POI không cần credential Azure.</summary>
    public AzureBlobAudioStorageService(
        Lazy<BlobContainerClient> containerClient,
        IValidator<StoreAudioRequest> validator,
        ILogger<AzureBlobAudioStorageService> logger)
    {
        _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AudioStorageResult> StoreAudioAsync(
        StoreAudioRequest command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var blob = Container.GetBlobClient(command.AssetKey);
        if (blob.Uri.Scheme != Uri.UriSchemeHttps)
            throw new AudioStorageException("Azure Blob production phải dùng HTTPS để truyền MP3 và credential an toàn.");
        if (blob.Uri.Query.Length > 0 || blob.Uri.Fragment.Length > 0)
            throw new AudioStorageException("Azure Blob client chứa URL có token; không được lưu SAS URL vào AudioGuide.");
        if (blob.Uri.AbsoluteUri.Length > 500)
            throw new AudioStorageException("Định danh Blob vượt quá giới hạn AudioGuide.AudioAssetUri (500 ký tự).");

        using var stream = new MemoryStream(command.AudioData, writable: false);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "audio/mpeg" },
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        };

        try
        {
            await blob.UploadAsync(stream, uploadOptions, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException ex) when (ex.Status == 412 || ex.ErrorCode == "BlobAlreadyExists")
        {
            throw new AudioStorageException("AssetKey đã tồn tại; hãy dùng khóa phiên bản mới, không ghi đè MP3 đã publish.");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError("Azure Blob upload thất bại ({Status}, {ErrorCode}).", ex.Status, ex.ErrorCode);
            throw new AudioStorageException("Không thể lưu MP3 vào Azure Blob.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Azure Blob upload thất bại ({ExceptionType}).", ex.GetType().Name);
            throw new AudioStorageException("Không thể lưu MP3 vào Azure Blob.");
        }

        return new AudioStorageResult(
            blob.Uri.AbsoluteUri,
            "audio/mpeg",
            command.AudioData.LongLength,
            DateTimeOffset.UtcNow);
    }

    public async Task<bool> ExistsAsync(
        string assetIdentifier,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(assetIdentifier)) return false;

        string assetKey = assetIdentifier;
        if (Uri.TryCreate(assetIdentifier, UriKind.Absolute, out var uri))
        {
            var prefix = Container.Uri.AbsoluteUri.TrimEnd('/') + "/";
            if (uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
                !assetIdentifier.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            assetKey = Uri.UnescapeDataString(assetIdentifier[prefix.Length..]);
        }

        if (assetKey.Length > 400 || !SafeAssetKey.IsMatch(assetKey)) return false;

        try
        {
            return (await Container.GetBlobClient(assetKey).ExistsAsync(cancellationToken)).Value;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError("Azure Blob existence check thất bại ({Status}, {ErrorCode}).", ex.Status, ex.ErrorCode);
            throw new AudioStorageException("Không thể kiểm tra MP3 trong Azure Blob.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Azure Blob existence check thất bại ({ExceptionType}).", ex.GetType().Name);
            throw new AudioStorageException("Không thể kiểm tra MP3 trong Azure Blob.");
        }
    }

    public async Task<StoredAudio?> ReadAudioAsync(
        string assetKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(assetKey) || assetKey.Length > 400 || !SafeAssetKey.IsMatch(assetKey))
            return null;

        try
        {
            var content = await Container.GetBlobClient(assetKey).DownloadContentAsync(cancellationToken);
            return new StoredAudio(content.Value.Content.ToArray(), "audio/mpeg");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError("Azure Blob download thất bại ({Status}, {ErrorCode}).", ex.Status, ex.ErrorCode);
            throw new AudioStorageException("Không thể đọc MP3 từ Azure Blob.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Azure Blob download thất bại ({ExceptionType}).", ex.GetType().Name);
            throw new AudioStorageException("Không thể đọc MP3 từ Azure Blob.");
        }
    }
}
