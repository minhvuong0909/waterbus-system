using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Validators.Bookings;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Features.Bookings.Services;
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
            try { await CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c)).CreateAsync(command, default); return true; }
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
        var request = new CreateCheckoutRequest("Buyer", "buyer@example.com", null,
            [new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id),
             new(Guid.NewGuid(), [seed.Seat2.Id], seed.StationA.Id, seed.StationC.Id)]);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.CreateAsync(request, default));
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
        await CommercialHandlerFactory.Checkout(c, new SqlCommercialTransactionRunner(c), sessions).CreateAsync(new("Buyer", "buyer@example.com", null,
            [new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id),
             new(returnTrip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id)]), default);
        store.Writes.Should().Be(1); store.VisibleBookings.Should().Be(2);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentRefundRequestsSendOneGatewayCallAndShareRefundId()
    {
        await using var c = Context(); var seed = TestDbContextFactory.SeedTrip(c);
        var booking = await CommercialCheckoutTests.Builder(c, new SqlCommercialTransactionRunner(c)).CreateAsync(CommercialCheckoutTests.Command(seed), default);
        var gateway = new CommercialCheckoutTests.Gateway();
        var order = await c.PurchaseOrders.FirstAsync();
        c.FinancialTransactions.Add(new WaterbusSystem.Domain.Entities.FinancialTransaction
        { Order = order, OrderId = order.Id, Amount = order.QuotedTotal, GatewayName = "VNPAY", MerchantReference = "sql-payment",
          IdempotencyKey = "PAY_sql", GatewayRequestCreatedAt = DateTimeOffset.UtcNow });
        await c.SaveChangesAsync();
        var payment = await c.FinancialTransactions.FirstAsync();
        await CommercialHandlerFactory.Ipn(c, gateway, transactions: new SqlCommercialTransactionRunner(c)).ConfirmIpnAsync(CommercialCheckoutTests.Ipn(payment), default);
        seed.Trip.Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        async Task<Guid> Attempt()
        {
            await using var own = Context();
            var result = await CommercialHandlerFactory.Refund(own, [gateway], new SqlCommercialTransactionRunner(own))
                .RequestAsync(new(booking.BookingId, "cancelled"), default);
            return result.RefundTransactionId;
        }
        var ids = await Task.WhenAll(Attempt(), Attempt());
        ids[0].Should().Be(ids[1]); gateway.RefundCalls.Should().Be(1);
    }

    [SqlServerDomainFact]
    public async Task OccupiedSeatUpdateRollsBackOriginalReservationAndPreservesCache()
    {
        await using var context = Context();
        var seed = TestDbContextFactory.SeedTrip(context);
        var store = new CommercialHandlerFactory.MemoryStore();
        var sessions = new CheckoutSessionService(store, new DateTimeService(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));
        var checkout = CommercialHandlerFactory.Checkout(context, new SqlCommercialTransactionRunner(context), sessions);
        var hold = await checkout.HoldAsync(new([new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id)]));
        var blocker = await CommercialHandlerFactory.Booking(context, transactions: new SqlCommercialTransactionRunner(context))
            .CreateAsync(CommercialCheckoutTests.Command(seed) with { SeatIds = [seed.Seat2.Id] });
        var originalCache = store.Items.ToDictionary(p => p.Key, p => p.Value);
        await Assert.ThrowsAsync<ConcurrencyException>(() => checkout.UpdateSeatsAsync(hold.OrderId,
            hold.Bookings.Single().BookingId, new([seed.Seat2.Id], [new("Buyer", null, null)]), hold.AccessToken));
        await using var verify = Context();
        var reservations = await verify.SeatReservations.AsNoTracking().ToListAsync();
        reservations.Should().HaveCount(2);
        reservations.Single(r => r.BookingId == hold.Bookings.Single().BookingId).SeatId.Should().Be(seed.Seat1.Id);
        reservations.Should().OnlyContain(r => r.Status == ReservationStatus.Pending);
        (await verify.PurchaseOrders.SingleAsync(o => o.Id == hold.OrderId)).ExpiresAt.Should().Be(hold.ExpiresAt);
        store.Items.Should().BeEquivalentTo(originalCache);
    }

    [SqlServerDomainFact]
    public async Task SuccessfulSeatUpdateCommitsNewReservationAndKeepsDeadline()
    {
        await using var context = Context();
        var seed = TestDbContextFactory.SeedTrip(context);
        var checkout = CommercialHandlerFactory.Checkout(context, new SqlCommercialTransactionRunner(context));
        var hold = await checkout.HoldAsync(new([new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id)]));
        await checkout.UpdateSeatsAsync(hold.OrderId, hold.Bookings.Single().BookingId,
            new([seed.Seat2.Id], [new("Replacement", null, null)]), hold.AccessToken);
        await using var verify = Context();
        var reservations = await verify.SeatReservations.AsNoTracking().ToListAsync();
        reservations.Single(r => r.SeatId == seed.Seat1.Id).Status.Should().Be(ReservationStatus.Cancelled);
        reservations.Single(r => r.SeatId == seed.Seat2.Id).Status.Should().Be(ReservationStatus.Pending);
        (await verify.PurchaseOrders.SingleAsync()).ExpiresAt.Should().Be(hold.ExpiresAt);
        (await verify.Tickets.CountAsync()).Should().Be(0);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentPaymentConfirmationsIssueTicketsOnceAcrossScopes()
    {
        await using var context = Context();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await CommercialHandlerFactory.Booking(context, transactions: new SqlCommercialTransactionRunner(context))
            .CreateAsync(CommercialCheckoutTests.Command(seed));
        var gateway = new CommercialCheckoutTests.Gateway();
        await CommercialHandlerFactory.Payment(context, gateway, new SqlCommercialTransactionRunner(context))
            .CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var query = CommercialCheckoutTests.Ipn(await context.FinancialTransactions.SingleAsync());
        async Task<string> Confirm()
        {
            await using var own = Context();
            return (await CommercialHandlerFactory.Payment(own, gateway, new SqlCommercialTransactionRunner(own))
                .ConfirmIpnAsync(query)).RspCode;
        }
        var responses = await Task.WhenAll(Confirm(), Confirm());
        responses.Should().BeEquivalentTo(["00", "02"]);
        await using var verify = Context();
        (await verify.Tickets.CountAsync()).Should().Be(1);
        (await verify.PurchaseOrders.SingleAsync()).Status.Should().Be(OrderStatus.Confirmed);
    }
}
