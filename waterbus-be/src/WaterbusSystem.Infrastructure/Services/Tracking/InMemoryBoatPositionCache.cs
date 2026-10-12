using System.Collections.Concurrent;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.Tracking;

/// <summary>
/// Cache vị trí trong bộ nhớ tiến trình. Dùng làm dự phòng khi Redis không sẵn sàng và trong unit test.
/// Chỉ giữ điểm có ObservedAt mới hơn điểm đang lưu.
/// </summary>
public class InMemoryBoatPositionCache : IBoatPositionCache
{
    private readonly ConcurrentDictionary<Guid, BoatPositionMessage> _positions = new();

    public Task<BoatPositionMessage?> GetAsync(Guid boatId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_positions.TryGetValue(boatId, out var message) ? message : null);

    public Task<IReadOnlyList<BoatPositionMessage>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BoatPositionMessage>>(_positions.Values.ToList());

    public Task SetAsync(BoatPositionMessage message, CancellationToken cancellationToken = default)
    {
        _positions.AddOrUpdate(message.BoatId, message,
            (_, existing) => message.ObservedAt > existing.ObservedAt ? message : existing);
        return Task.CompletedTask;
    }
}
