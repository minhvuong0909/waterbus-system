namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>Mô tả một clip audio đã publish (không kèm dữ liệu MP3). Một thông báo gồm các clip "vi" rồi "en".</summary>
public sealed record AudioClipRef(string Language, int DurationSeconds, string Text)
{
    /// <summary>Sắp xếp "vi" trước "en"; rỗng nếu thiếu bản tiếng Việt (không phát thông báo thiếu "vi").</summary>
    public static IReadOnlyList<AudioClipRef> VietnameseThenEnglish(IEnumerable<AudioClipRef> clips)
    {
        var list = clips.ToList();
        var vi = list.FirstOrDefault(c => string.Equals(c.Language, "vi", StringComparison.OrdinalIgnoreCase));
        if (vi is null) return Array.Empty<AudioClipRef>();
        var en = list.FirstOrDefault(c => string.Equals(c.Language, "en", StringComparison.OrdinalIgnoreCase));
        return en is null ? [vi] : [vi, en];
    }
}
