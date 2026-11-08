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

    public Task<bool> TryReserveAsync(Guid tripId, Guid poiId, string? visitId = null, CancellationToken cancellationToken = default) =>
        repository.TryReserveAsync(AudioRequestIds.For(tripId, poiId, visitId), tripId, poiId, visitId ?? string.Empty,
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(Options.ReservationLeaseSeconds), Options.MaxDispatchAttempts, cancellationToken);

    public Task MarkAsTriggeredAsync(Guid tripId, Guid poiId, string? visitId = null, CancellationToken cancellationToken = default) =>
        repository.MarkAcceptedAsync(AudioRequestIds.For(tripId, poiId, visitId), DateTimeOffset.UtcNow, cancellationToken);

    public Task ReleaseReservationAsync(Guid tripId, Guid poiId, string? visitId = null, CancellationToken cancellationToken = default) =>
        repository.ReleaseAsync(AudioRequestIds.For(tripId, poiId, visitId), "NotAcceptedByPlayer", Options.MaxDispatchAttempts, cancellationToken);
}
