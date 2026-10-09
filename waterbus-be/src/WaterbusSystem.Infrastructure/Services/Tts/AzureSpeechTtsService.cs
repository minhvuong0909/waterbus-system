using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Infrastructure.Services.Tts;

/// <summary>
/// Dịch vụ tổng hợp giọng nói sử dụng Azure AI Speech Neural Voice (MP3 tiếng Việt).
/// Đọc cấu hình từ Options (User Secrets / Environment Variables).
/// </summary>
public class AzureSpeechTtsService : ITextToSpeechService
{
    private readonly IOptions<AzureSpeechOptions> _options;
    private readonly IAzureSpeechSynthesizerClient _client;
    private readonly ILogger<AzureSpeechTtsService> _logger;

    public AzureSpeechTtsService(
        IOptions<AzureSpeechOptions> options,
        IAzureSpeechSynthesizerClient client,
        ILogger<AzureSpeechTtsService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AudioSynthesisResult> SynthesizeSpeechAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate dữ liệu đầu vào
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Văn bản thuyết minh không được để trống.", nameof(text));
        }
        cancellationToken.ThrowIfCancellationRequested();

        // 2. Kiểm tra cấu hình bắt buộc
        var config = _options.Value;
        if (string.IsNullOrWhiteSpace(config.SubscriptionKey))
        {
            throw new InvalidOperationException(
                "Thiếu cấu hình AzureSpeech:SubscriptionKey. Hãy cấu hình qua User Secrets (local) hoặc biến môi trường (deploy), không hard-code trong appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(config.Region))
        {
            throw new InvalidOperationException(
                "Thiếu cấu hình AzureSpeech:Region. Hãy cấu hình qua User Secrets (local) hoặc biến môi trường (deploy).");
        }

        var voiceName = options?.VoiceName ?? config.DefaultVoiceName;
        if (string.IsNullOrWhiteSpace(voiceName))
            throw new InvalidOperationException("Thiếu cấu hình AzureSpeech:DefaultVoiceName (giọng vi-VN Neural).");
        if (!voiceName.StartsWith("vi-VN-", StringComparison.OrdinalIgnoreCase)
            || !voiceName.EndsWith("Neural", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Giọng TTS phải là Azure Neural Voice tiếng Việt (vi-VN).", nameof(options));

        var outputFormat = options?.OutputFormat ?? config.OutputFormat;
        if (string.IsNullOrWhiteSpace(outputFormat))
            throw new InvalidOperationException("Thiếu cấu hình AzureSpeech:OutputFormat (MP3).");
        AzureSpeechFormat.ParseMp3(outputFormat);

        if (config.TimeoutSeconds <= 0)
            throw new InvalidOperationException("AzureSpeech:TimeoutSeconds phải dương.");
        var timeout = options?.Timeout ?? TimeSpan.FromSeconds(config.TimeoutSeconds);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Timeout phải dương.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        SpeechSynthesisResultData resultData;
        try
        {
            resultData = await _client.SynthesizeAsync(
                text,
                voiceName,
                outputFormat,
                config.SubscriptionKey,
                config.Region,
                cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && cts.IsCancellationRequested)
        {
            _logger.LogWarning("Quá trình tổng hợp giọng nói Azure Speech bị quá thời gian chờ ({TimeoutSeconds}s).", timeout.TotalSeconds);
            throw new TextToSpeechException($"Yêu cầu tổng hợp giọng nói bị quá thời gian chờ ({timeout.TotalSeconds}s).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Tác vụ tổng hợp giọng nói bị hủy bởi người gọi.");
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new TextToSpeechException("Azure Speech đã hủy yêu cầu tổng hợp.", "ProviderCanceled");
        }
        catch (Exception ex)
        {
            // SDK exception có thể mang thông tin request/credential trong Message.
            _logger.LogError("Azure Speech SDK thất bại ({ExceptionType}).", ex.GetType().Name);
            throw new TextToSpeechException("Không thể hoàn thành tổng hợp giọng nói từ Azure Speech.", "ProviderError");
        }

        // 3. Xử lý phản hồi từ provider
        if (!resultData.Succeeded)
        {
            var safeCode = resultData.ErrorCode switch
            {
                "AuthenticationFailure" or "ConnectionFailure" or "ServiceError"
                    or "ServiceTimeout" or "BadRequest" or "Forbidden"
                    or "TooManyRequests" or "RuntimeError" => resultData.ErrorCode,
                _ => "ProviderError"
            };
            _logger.LogError("Azure Speech trả về lỗi ({ErrorCode}).", safeCode);
            throw new TextToSpeechException("Azure Speech tổng hợp thất bại.", safeCode);
        }

        if (resultData.AudioData == null || resultData.AudioData.Length == 0)
        {
            _logger.LogError("Azure Speech trả về kết quả thành công nhưng dữ liệu âm thanh rỗng.");
            throw new TextToSpeechException("Azure Speech trả về dữ liệu âm thanh rỗng.");
        }

        return new AudioSynthesisResult(
            AudioData: resultData.AudioData,
            ContentType: "audio/mpeg",
            Duration: resultData.Duration,
            VoiceName: voiceName,
            ByteLength: resultData.AudioData.Length);
    }
}
