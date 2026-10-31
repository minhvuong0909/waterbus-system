using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Validators.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Services.SightseeingAudio;
using WaterbusSystem.Infrastructure.Services.AudioStorage;
using WaterbusSystem.Infrastructure.Services.Tts;
using WaterbusSystem.WebApi.Controllers;
using Xunit;
using Xunit.Abstractions;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

/// <summary>
/// Chỉ khi bật cờ mới gọi Azure Speech và Blob thật. File MP3 tải lại được giữ
/// ngoài repo để người chạy nghe; blob thử chỉ được giữ khi bật cờ riêng.
/// </summary>
public sealed class AzureAudioPipelineLiveSmokeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task GenerateUploadAndDownloadRealVietnameseMp3_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("WATERBUS_AZURE_AUDIO_PIPELINE_SMOKE") != "1")
            return;

        var keepBlob = Environment.GetEnvironmentVariable("WATERBUS_KEEP_AUDIO_SMOKE_BLOB") == "1";

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(BaseApiController).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var speechOptions = new AzureSpeechOptions
        {
            SubscriptionKey = RequireConfiguration(configuration, "AzureSpeech:SubscriptionKey"),
            Region = RequireConfiguration(configuration, "AzureSpeech:Region"),
            DefaultVoiceName = RequireConfiguration(configuration, "AzureSpeech:DefaultVoiceName"),
            OutputFormat = RequireConfiguration(configuration, "AzureSpeech:OutputFormat"),
            TimeoutSeconds = 60
        };
        var container = new BlobContainerClient(
            RequireConfiguration(configuration, "AzureBlobAudioStorage:ConnectionString"),
            RequireConfiguration(configuration, "AzureBlobAudioStorage:ContainerName"));
        var actualSpeech = new AzureSpeechTtsService(
            Options.Create(speechOptions),
            new DefaultAzureSpeechSynthesizerClient(),
            NullLogger<AzureSpeechTtsService>.Instance);
        var capturedSpeech = new CapturingTextToSpeechService(actualSpeech);
        var storage = new AzureBlobAudioStorageService(
            container,
            new StoreAudioRequestValidator(),
            NullLogger<AzureBlobAudioStorageService>.Instance);
        var pipeline = new AudioAssetGenerationService(capturedSpeech, storage);

        var poiId = Guid.NewGuid();
        var assetKey = $"smoke-tests/{poiId:N}/vi/v1.mp3";
        var blob = container.GetBlobClient(assetKey);
        var uploaded = false;
        var verified = false;

        try
        {
            var result = await pipeline.GenerateAsync(new GenerateAudioAssetRequest(
                poiId,
                "Chào mừng quý khách đến với chuyến tham quan sông Sài Gòn.",
                assetKey));
            uploaded = true;

            result.ContentType.Should().Be("audio/mpeg");
            result.DurationSeconds.Should().BeGreaterThan(0);
            result.VoiceName.Should().Be(speechOptions.DefaultVoiceName);
            result.DurableAssetUri.Should().Be(blob.Uri.AbsoluteUri);
            result.DurableAssetUri.Should().NotContain("?");

            var downloaded = await blob.DownloadContentAsync();
            var downloadedBytes = downloaded.Value.Content.ToArray();
            downloaded.Value.Details.ContentType.Should().Be("audio/mpeg");
            downloadedBytes.Should().Equal(capturedSpeech.AudioData);
            result.ByteLength.Should().Be(downloadedBytes.LongLength);

            var outputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Waterbus", "smoke");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"azure-audio-{poiId:N}.mp3");
            await File.WriteAllBytesAsync(outputPath, downloadedBytes);
            File.Exists(outputPath).Should().BeTrue();
            verified = true;

            output.WriteLine($"MP3 local: {outputPath}");
            if (keepBlob)
            {
                output.WriteLine($"Đã giữ blob private: {blob.Uri.AbsoluteUri}");
                output.WriteLine($"Asset key: {assetKey}");
            }
        }
        finally
        {
            // Chỉ giữ blob sau khi toàn bộ kiểm chứng thành công và người chạy chủ động bật cờ.
            if (uploaded && !(keepBlob && verified))
                await blob.DeleteIfExistsAsync();
        }
    }

    private static string RequireConfiguration(IConfiguration configuration, string name) =>
        !string.IsNullOrWhiteSpace(configuration[name])
            ? configuration[name]!
            : throw new InvalidOperationException($"Thiếu {name} trong WebApi User Secrets hoặc biến môi trường.");

    private sealed class CapturingTextToSpeechService(ITextToSpeechService inner) : ITextToSpeechService
    {
        public byte[] AudioData { get; private set; } = Array.Empty<byte>();

        public async Task<AudioSynthesisResult> SynthesizeSpeechAsync(
            string text,
            TextToSpeechOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var result = await inner.SynthesizeSpeechAsync(text, options, cancellationToken);
            AudioData = result.AudioData.ToArray();
            return result;
        }
    }
}
