using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Interfaces.Repositories;

namespace WaterbusSystem.Infrastructure.Services.SightseeingAudio;

/// <summary>
/// Chống phát trùng bền vững (BR-EXP-05) trên sổ cái SQL: sống qua restart và đúng với nhiều instance.
/// Thay thế bản in-memory (chỉ còn dùng cho test/dev đơn lẻ).
/// </summary>
public sealed class SqlPoiTriggerDeduplicationService(IAudioPlaybackRepository repository, IOptions<SightseeingAudioOptions> options)
    : IPoiTriggerDeduplicationService
{
    private SightseeingAudioOptions Options => options.Value;

    public Task<bool> HasBeenTriggeredAsync(Guid tripId, Guid poiId, string? visitId = null, CancellationToken cancellationToken = default) =>
        repository.IsTriggeredAsync(tripId, poiId, visitId ?? string.Empty, cancellationToken);

    // Scoped theo evaluation: nhớ epoch của reservation đã giành được để các bước sau (accept/release) mang đúng fencing token.
    private readonly Dictionary<Guid, int> _epochs = new();

    public async Task<bool> TryReserveAsync(Guid tripId, Guid poiId, string? visitId = null, CancellationToken cancellationToken = default)
    {
        var id = AudioRequestIds.For(tripId, poiId, visitId);
        var epoch = await repository.TryReserveAsync(id, tripId, poiId, visitId ?? string.Empty, DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(Options.ReservationLeaseSeconds), Options.MaxDispatchAttempts, cancellationToken);
        if (epoch is null) return false;
        _epochs[id] = epoch.Value;
        return true;
    }

    public int? GetReservationEpoch(Guid tripId, Guid poiId, string? visitId = null) =>
        _epochs.TryGetValue(AudioRequestIds.For(tripId, poiId, visitId), out var e) ? e : null;

    public async Task MarkAsTriggeredAsync(Guid tripId, Guid poiId, string? visitId = null, CancellationToken cancellationToken = default)
    {
        var id = AudioRequestIds.For(tripId, poiId, visitId);
        if (_epochs.TryGetValue(id, out var epoch))
            await repository.MarkAcceptedAsync(id, epoch, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task ReleaseReservationAsync(Guid tripId, Guid poiId, string? visitId = null, CancellationToken cancellationToken = default)
    {
        var id = AudioRequestIds.For(tripId, poiId, visitId);
        // Không có epoch = chưa từng giữ reservation trong evaluation này ⇒ không được nhả của người khác.
        if (_epochs.TryGetValue(id, out var epoch))
            await repository.ReleaseAsync(id, epoch, "NotAcceptedByPlayer", Options.MaxDispatchAttempts, cancellationToken);
    }
}
