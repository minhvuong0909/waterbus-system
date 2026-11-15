using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Một clip MP3 (một ngôn ngữ) của thông báo bến hoặc thuyết minh địa danh, lưu trực tiếp trong SQL.
/// Một thông báo gồm các clip cùng (Kind, SubjectId): "vi" phát trước rồi "en".
/// </summary>
public class AudioClip : BaseEntity
{
    public AudioClipKind Kind { get; set; }

    /// <summary>StationId (Kind bến) hoặc PoiId (Kind Poi).</summary>
    public Guid SubjectId { get; set; }

    /// <summary>"vi" hoặc "en".</summary>
    public string LanguageCode { get; set; } = "vi";

    /// <summary>Nội dung văn bản của clip (hiển thị trên màn hình Captain).</summary>
    public string Text { get; set; } = string.Empty;

    public string ContentType { get; set; } = "audio/mpeg";
    public byte[] Data { get; set; } = [];
    public int DurationSeconds { get; set; }
    public bool IsPublished { get; set; } = true;
}
