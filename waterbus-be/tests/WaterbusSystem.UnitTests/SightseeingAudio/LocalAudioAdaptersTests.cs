using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Infrastructure.Services.SightseeingAudio;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class LocalAudioAdaptersTests
{
    [Fact]
    public async Task LogOnlyDispatcher_ShouldNotClaimPlayerAcknowledged()
    {
        var dispatcher = new LogOnlyAudioTriggerDispatcher(
            NullLogger<LogOnlyAudioTriggerDispatcher>.Instance);

        var result = await dispatcher.DispatchTriggerRequestAsync(new AudioTriggerRequest
        {
            RequestId = Guid.NewGuid(), PoiName = "Bến Nhà Rồng"
        });

        result.IsDispatched.Should().BeTrue();
        result.IsAcceptedByPlayer.Should().BeFalse();
    }

    [Fact]
    public async Task Deduplication_ShouldReleaseReservationButKeepTriggeredState()
    {
        var dedup = new InMemoryPoiTriggerDeduplicationService();
        var tripId = Guid.NewGuid();
        var routePoiId = Guid.NewGuid();

        (await dedup.TryReserveAsync(tripId, routePoiId)).Should().BeTrue();
        (await dedup.TryReserveAsync(tripId, routePoiId)).Should().BeFalse();
        await dedup.ReleaseReservationAsync(tripId, routePoiId);
        (await dedup.TryReserveAsync(tripId, routePoiId)).Should().BeTrue();
        await dedup.MarkAsTriggeredAsync(tripId, routePoiId);
        await dedup.ReleaseReservationAsync(tripId, routePoiId);

        (await dedup.HasBeenTriggeredAsync(tripId, routePoiId)).Should().BeTrue();
        (await dedup.TryReserveAsync(tripId, routePoiId)).Should().BeFalse();
    }
}
