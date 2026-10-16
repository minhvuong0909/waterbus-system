using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckout;
using WaterbusSystem.Application.Features.Bookings.Commands.ExpireCheckoutHolds;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;
using WaterbusSystem.Application.Features.Refunds.Commands.RequestRefund;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CommercialSqlTests : IAsyncLifetime
{
    private string? _connection;
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);
    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = "WaterbusDomainTests_Commercial_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var c = Context(); await c.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_Commercial_")) throw new InvalidOperationException("Unsafe test database.");
        await using var c = Context(); await c.Database.EnsureDeletedAsync();
    }

    [SqlServerDomainFact]
    public async Task ConcurrentBookingWithoutSharedRedisMutexStillHasOneWinner()
    {
        await using var seedContext = Context(); var seed = TestDbContextFactory.SeedTrip(seedContext);
        var command = CommercialCheckoutTests.Command(seed);
        async Task<bool> Attempt()
        {
            await using var c = Context();
            try { await CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c)).Handle(command, default); return true; }
            catch (ConcurrencyException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        results.Count(x => x).Should().Be(1);
        await using var check = Context(); (await check.SeatReservations.CountAsync()).Should().Be(1);
    }

    [SqlServerDomainFact]
    public async Task RoundTripFailureRollsBackFirstLegAndItsAccessGrant()
    {
        await using var c = Context(); var seed = TestDbContextFactory.SeedTrip(c);
        var runner = new SqlCommercialTransactionRunner(c);
        var store = new CommercialHandlerFactory.MemoryStore();
        var sessions = new CheckoutSessionService(store, new DateTimeService(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));
        var handler = CommercialHandlerFactory.Checkout(c, runner, sessions);
        var request = new CreateCheckoutCommand("Buyer", "buyer@example.com", null,
            [new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id),
             new(Guid.NewGuid(), [seed.Seat2.Id], seed.StationA.Id, seed.StationC.Id)]);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(request, default));
        (await c.PurchaseOrders.CountAsync()).Should().Be(0); (await c.AccessGrants.CountAsync()).Should().Be(0);
        (await c.SeatReservations.CountAsync()).Should().Be(0);
        store.Items.Should().BeEmpty("rollback không được ghi hold Redis của leg đầu");
    }

    private sealed class CommitObservingStore(Func<Task<int>> bookingsVisible) : ICheckoutSessionStore
    {
        public int Writes; public int VisibleBookings;
        public Task<string?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public async Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
        { Writes++; VisibleBookings = await bookingsVisible(); }
        public Task<bool> RemoveAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
    }

    [SqlServerDomainFact]
    public async Task RoundTripCacheIsWrittenOnceAfterBothBookingsAreVisibleToAnotherConnection()
    {
        await using var c = Context(); var seed = TestDbContextFactory.SeedTrip(c);
        var returnTrip = new WaterbusSystem.Domain.Entities.Trip { RouteId = seed.Route.Id, BoatId = seed.Boat.Id,
            DepartureTime = DateTimeOffset.UtcNow.AddDays(2), ArrivalTime = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30) };
        foreach (var call in seed.Trip.TripStopCalls)
            returnTrip.TripStopCalls.Add(new WaterbusSystem.Domain.Entities.TripStopCall { TripId = returnTrip.Id,
                RouteStopId = call.RouteStopId, VisitOrder = call.VisitOrder });
        c.Trips.Add(returnTrip); await c.SaveChangesAsync();
        var store = new CommitObservingStore(async () => { await using var separate = Context(); return await separate.Bookings.CountAsync(); });
        var sessions = new CheckoutSessionService(store, new DateTimeService(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));
        await CommercialHandlerFactory.Checkout(c, new SqlCommercialTransactionRunner(c), sessions).Handle(new("Buyer", "buyer@example.com", null,
            [new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id),
             new(returnTrip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id)]), default);
        store.Writes.Should().Be(1); store.VisibleBookings.Should().Be(2);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentRefundRequestsSendOneGatewayCallAndShareRefundId()
    {
        await using var c = Context(); var seed = TestDbContextFactory.SeedTrip(c);
        var booking = await CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c)).Handle(CommercialCheckoutTests.Command(seed), default);
        var gateway = new CommercialCheckoutTests.Gateway();
        var order = await c.PurchaseOrders.FirstAsync();
        c.FinancialTransactions.Add(new WaterbusSystem.Domain.Entities.FinancialTransaction
        { Order = order, OrderId = order.Id, Amount = order.QuotedTotal, GatewayName = "VNPAY", MerchantReference = "sql-payment",
          IdempotencyKey = "PAY_sql", GatewayRequestCreatedAt = DateTimeOffset.UtcNow });
        await c.SaveChangesAsync();
        var payment = await c.FinancialTransactions.FirstAsync();
        await CommercialHandlerFactory.Ipn(c, gateway, transactions: new SqlCommercialTransactionRunner(c)).Handle(CommercialCheckoutTests.Ipn(payment), default);
        seed.Trip.Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        async Task<Guid> Attempt()
        {
            await using var own = Context();
            var result = await CommercialHandlerFactory.Refund(own, [gateway], new SqlCommercialTransactionRunner(own))
                .Handle(new(booking.BookingId, "cancelled"), default);
            return result.RefundTransactionId;
        }
        var ids = await Task.WhenAll(Attempt(), Attempt());
        ids[0].Should().Be(ids[1]); gateway.RefundCalls.Should().Be(1);
    }
}
