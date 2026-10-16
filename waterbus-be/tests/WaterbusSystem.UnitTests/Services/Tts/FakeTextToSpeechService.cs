using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.UnitTests.Services.Tts;

/// <summary>
/// Fake chỉ dành cho unit test. Dữ liệu trả về không phải file MP3 phát được.
/// </summary>
public class FakeTextToSpeechService : ITextToSpeechService
{
    private static readonly byte[] FakeMp3Header = new byte[]
    {
        0xFF, 0xFB, 0x90, 0x64, // MP3 sync word & header sample
        0x00, 0x00, 0x00, 0x00, 0x54, 0x41, 0x47, 0x46 // ID3 tag dummy
    };

    public Task<AudioSynthesisResult> SynthesizeSpeechAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Văn bản thuyết minh không được để trống.", nameof(text));
        }

        var voice = options?.VoiceName ?? "vi-VN-HoaiMyNeural";

        // Ước lượng thời lượng: trung bình ~150 từ/phút (2.5 từ/giây), tối thiểu 2 giây
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var seconds = Math.Max(2.0, wordCount / 2.5);
        var duration = TimeSpan.FromSeconds(seconds);

        // Sinh mảng byte giả lập kích thước tương ứng (~16KB/giây cho 128kbps)
        var byteCount = (int)(seconds * 16_000);
        var dummyData = new byte[byteCount];
        Array.Copy(FakeMp3Header, dummyData, Math.Min(FakeMp3Header.Length, dummyData.Length));

        var result = new AudioSynthesisResult(
            AudioData: dummyData,
            ContentType: "audio/mpeg",
            Duration: duration,
            VoiceName: voice,
            ByteLength: dummyData.Length);

        return Task.FromResult(result);
    }
}
