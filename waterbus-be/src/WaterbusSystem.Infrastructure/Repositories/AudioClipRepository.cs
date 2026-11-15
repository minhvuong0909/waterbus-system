using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class AudioClipRepository(ApplicationDbContext context) : IAudioClipRepository
{
    public Task<StoredAudio?> ReadAsync(AudioClipKind kind, Guid subjectId, string language, CancellationToken ct) =>
        context.AudioClips.AsNoTracking()
            .Where(c => c.Kind == kind && c.SubjectId == subjectId && c.LanguageCode == language && c.IsPublished && !c.IsDeleted)
            .Select(c => new StoredAudio(c.Data, c.ContentType))
            .FirstOrDefaultAsync(ct);
}
