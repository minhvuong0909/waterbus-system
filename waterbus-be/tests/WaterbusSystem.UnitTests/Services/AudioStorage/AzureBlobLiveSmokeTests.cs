using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Common.Validators;
using WaterbusSystem.Infrastructure.Services.AudioStorage;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.Services.AudioStorage;

/// <summary>
/// Kiểm tra Azure Blob thật khi người chạy chủ động bật cờ. Bytes thử chỉ có
/// chữ ký ID3, không phải MP3 nghe được; test này xác nhận đường upload/đọc lại.
/// </summary>
public sealed class AzureBlobLiveSmokeTests
{
    [Fact]
    public async Task UploadAndReadBack_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("WATERBUS_AZURE_BLOB_SMOKE") != "1")
            return;

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(BaseApiController).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = RequireConfiguration(configuration, "AzureBlobAudioStorage:ConnectionString");
        var containerName = RequireConfiguration(configuration, "AzureBlobAudioStorage:ContainerName");
        var container = new BlobContainerClient(connectionString, containerName);
        var storage = new AzureBlobAudioStorageService(
            container,
            new StoreAudioCommandValidator(),
            NullLogger<AzureBlobAudioStorageService>.Instance);

        // GUID mới: tuyệt đối không đụng vào AudioGuide/MP3 đã publish.
        var assetKey = $"smoke-tests/{Guid.NewGuid():N}.mp3";
        byte[] bytes = [(byte)'I', (byte)'D', (byte)'3', 4, 0, 0, 0, 0, 0, 0];
        var blob = container.GetBlobClient(assetKey);
        var uploaded = false;

        try
        {
            var stored = await storage.StoreAudioAsync(new StoreAudioCommand
            {
                AssetKey = assetKey,
                AudioData = bytes,
                ContentType = "audio/mpeg",
                LanguageCode = "vi"
            });
            uploaded = true;

            stored.DurableAssetUri.Should().Be(blob.Uri.AbsoluteUri);
            stored.DurableAssetUri.Should().NotContain("?");
            (await storage.ExistsAsync(stored.DurableAssetUri)).Should().BeTrue();

            var downloaded = await blob.DownloadContentAsync();
            downloaded.Value.Details.ContentType.Should().Be("audio/mpeg");
            downloaded.Value.Content.ToArray().Should().Equal(bytes);
        }
        finally
        {
            // Chỉ xóa blob có khóa GUID do chính test này tạo.
            if (uploaded)
                await blob.DeleteIfExistsAsync();
        }
    }

    private static string RequireConfiguration(IConfiguration configuration, string name) =>
        !string.IsNullOrWhiteSpace(configuration[name])
            ? configuration[name]!
            : throw new InvalidOperationException($"Thiếu {name} trong WebApi User Secrets hoặc biến môi trường.");
}
