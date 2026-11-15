using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Đọc dữ liệu MP3 của clip đã publish (lưu trong SQL).</summary>
public interface IAudioClipRepository
{
    Task<StoredAudio?> ReadAsync(AudioClipKind kind, Guid subjectId, string language, CancellationToken ct);
}
