using System.Collections.Concurrent;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.UnitTests.TestHelpers;

/// <summary>
/// Test double mô phỏng hành vi loại trừ lẫn nhau (mutual exclusion) thật của Redis RedLock
/// bằng SemaphoreSlim nội bộ, dùng để kiểm thử CreateBookingCommandHandler dưới điều kiện tranh chấp
/// (concurrent request) mà không cần một Redis server thật chạy trong CI.
/// </summary>
public class FakeDistributedLockService : IDistributedLockService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public Task<IAsyncDisposable?> AcquireLockAsync(
        string resourceKey,
        TimeSpan expiryTime,
        TimeSpan waitTime,
        TimeSpan retryTime,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(resourceKey, _ => new SemaphoreSlim(1, 1));

        return semaphore.Wait(0)
            ? Task.FromResult<IAsyncDisposable?>(new Releaser(semaphore))
            : Task.FromResult<IAsyncDisposable?>(null);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public ValueTask DisposeAsync()
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
