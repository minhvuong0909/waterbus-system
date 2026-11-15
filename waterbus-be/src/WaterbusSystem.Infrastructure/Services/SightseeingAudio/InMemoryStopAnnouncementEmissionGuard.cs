using System.Collections.Concurrent;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Infrastructure.Services.SightseeingAudio;

/// <summary>
/// Chống phát lặp thông báo bến trong tiến trình: mỗi (chuyến, chặng, loại thông báo) và mỗi
/// (chuyến, địa danh) chỉ được phát đúng một lần. Dùng chung cho lệnh vận hành và bộ mô phỏng.
/// </summary>
public sealed class InMemoryStopAnnouncementEmissionGuard : IStopAnnouncementEmissionGuard
{
    private const int MaxTrackedEntries = 2000;

    private readonly ConcurrentDictionary<string, byte> _announced = new();
    private readonly ConcurrentDictionary<string, byte> _narrated = new();

    public bool TryClaim(Guid tripId, int segmentIndex, string announcementType) =>
        TryAdd(_announced, Key(tripId, segmentIndex, announcementType));

    public bool TryClaimNarration(Guid tripId, Guid poiId) =>
        TryAdd(_narrated, $"{tripId:N}:{poiId:N}");

    private bool TryAdd(ConcurrentDictionary<string, byte> store, string key)
    {
        if (store.TryAdd(key, 0)) return true;
        if (store.Count <= MaxTrackedEntries) return false;

        foreach (var stale in store.Keys.Take(MaxTrackedEntries / 2))
        {
            store.TryRemove(stale, out _);
        }
        return store.TryAdd(key, 0);
    }

    private static string Key(Guid tripId, int segmentIndex, string announcementType) =>
        $"{tripId:N}:{segmentIndex}:{announcementType}";
}
