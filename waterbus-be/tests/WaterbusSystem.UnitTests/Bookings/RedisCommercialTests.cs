using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Infrastructure.Locking;
using WaterbusSystem.Infrastructure.Services.Checkout;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CommercialRedisFactAttribute : FactAttribute
{
    public CommercialRedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WATERBUS_TEST_REDIS")))
            Skip = "Set WATERBUS_TEST_REDIS to semicolon-separated independent test Redis connections.";
    }
}
public sealed class RedisCommercialTests
{
    [CommercialRedisFact]
    public async Task CheckoutCacheAutomaticallyExpiresAtItsRedisTtl()
    {
        var endpoint = Environment.GetEnvironmentVariable("WATERBUS_TEST_REDIS")!.Split(';')[0];
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:RedisConnection"] = endpoint }).Build();
        using var store = new RedisCheckoutSessionStore(config, NullLogger<RedisCheckoutSessionStore>.Instance);
        var key = "waterbus-test-ttl-" + Guid.NewGuid().ToString("N");
        await store.SetAsync(key, "fixture", TimeSpan.FromMilliseconds(1200));
        (await store.GetAsync(key)).Should().Be("fixture");
        await Task.Delay(1600);
        (await store.GetAsync(key)).Should().BeNull();
    }

    [CommercialRedisFact]
    public async Task IndependentRedisNodesKeepExclusiveOwnershipAndReleaseForNextClient()
    {
        var values = Environment.GetEnvironmentVariable("WATERBUS_TEST_REDIS")!.Split(';');
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values.Select((v, i) =>
            new KeyValuePair<string, string?>("RedisLock:Connections:" + i, v))).Build();
        using var first = new RedisDistributedLockService(configuration, NullLogger<RedisDistributedLockService>.Instance);
        using var second = new RedisDistributedLockService(configuration, NullLogger<RedisDistributedLockService>.Instance);
        var resource = "waterbus-test-" + Guid.NewGuid().ToString("N");
        var lease = await first.AcquireLockAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(25));
        lease.Should().NotBeNull();
        var blocked = await second.AcquireLockAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(25));
        blocked.Should().BeNull();
        await lease!.DisposeAsync();
        var acquired = await second.AcquireLockAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(25));
        acquired.Should().NotBeNull(); await acquired!.DisposeAsync();
    }
}
