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
using WaterbusSystem.UnitTests.Payments;
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

    [SqlServerDomainFact]
    public async Task ConcurrentSuccessAfterFailurePreservesAnotherBuyersReservation()
    {
        await using var context = Context();
        var seed = TestDbContextFactory.SeedTrip(context);
        using var gateway = new VerifiedVnPayFixture();
        var runner = new SqlCommercialTransactionRunner(context);
        var booking = await CommercialHandlerFactory.Booking(context, transactions: runner)
            .CreateAsync(CommercialCheckoutTests.Command(seed));
        var payments = CommercialHandlerFactory.Payment(context, gateway.Gateway, runner);
        await payments.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var payment = await context.FinancialTransactions.SingleAsync();
        await payments.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount, "24", "02"));
        var replacement = await CommercialHandlerFactory.Booking(context, transactions: runner)
            .CreateAsync(CommercialCheckoutTests.Command(seed));
        var success = gateway.Callback(payment.MerchantReference!, payment.Amount);
        async Task<string> Confirm()
        {
            await using var own = Context();
            return (await CommercialHandlerFactory.Payment(own, gateway.Gateway, new SqlCommercialTransactionRunner(own))
                .ConfirmIpnAsync(success)).RspCode;
        }
        (await Task.WhenAll(Confirm(), Confirm())).Should().BeEquivalentTo(["00", "02"]);
        await using var verify = Context();
        var receipt = await verify.FinancialTransactions.SingleAsync();
        receipt.Status.Should().Be(FinancialTransactionStatus.Succeeded);
        receipt.FailureReason.Should().StartWith("ReconciliationRequired:");
        (await verify.PurchaseOrders.SingleAsync(o => o.Id == booking.OrderId)).Status.Should().Be(OrderStatus.Expired);
        (await verify.SeatReservations.SingleAsync(r => r.BookingId == booking.BookingId)).Status.Should().Be(ReservationStatus.Cancelled);
        (await verify.SeatReservations.SingleAsync(r => r.BookingId == replacement.BookingId)).Status.Should().Be(ReservationStatus.Pending);
        (await verify.Bookings.SingleAsync(b => b.Id == booking.BookingId)).PaidAllocation.Should().BeNull();
        (await verify.Tickets.CountAsync()).Should().Be(0);
    }

    [SqlServerDomainFact]
    public async Task ConcurrentContradictoryCallbacksNeverDowngradeReceivedMoneyOrDuplicateTickets()
    {
        await using var context = Context();
        var seed = TestDbContextFactory.SeedTrip(context);
        using var gateway = new VerifiedVnPayFixture();
        var runner = new SqlCommercialTransactionRunner(context);
        var booking = await CommercialHandlerFactory.Booking(context, transactions: runner)
            .CreateAsync(CommercialCheckoutTests.Command(seed));
        await CommercialHandlerFactory.Payment(context, gateway.Gateway, runner)
            .CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var payment = await context.FinancialTransactions.SingleAsync();
        async Task<string> Confirm(string response, string status)
        {
            await using var own = Context();
            return (await CommercialHandlerFactory.Payment(own, gateway.Gateway, new SqlCommercialTransactionRunner(own))
                .ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount, response, status))).RspCode;
        }
        var results = await Task.WhenAll(Confirm("24", "02"), Confirm("00", "00"));
        results[1].Should().Be("00");
        results[0].Should().BeOneOf("00", "99");
        (await Confirm("00", "00")).Should().Be("02");
        await using var verify = Context();
        (await verify.FinancialTransactions.SingleAsync()).Status.Should().Be(FinancialTransactionStatus.Succeeded);
        var order = await verify.PurchaseOrders.SingleAsync();
        var stored = await verify.Bookings.SingleAsync();
        var reservation = await verify.SeatReservations.SingleAsync();
        if (order.Status == OrderStatus.Confirmed)
        {
            (await verify.Tickets.CountAsync()).Should().Be(1);
            stored.PaidAllocation.Should().Be(payment.Amount);
            reservation.PaidAllocation.Should().Be(payment.Amount);
            reservation.Status.Should().Be(ReservationStatus.Confirmed);
        }
        else
        {
            order.Status.Should().Be(OrderStatus.Expired);
            (await verify.Tickets.CountAsync()).Should().Be(0);
            stored.PaidAllocation.Should().BeNull();
            reservation.Status.Should().Be(ReservationStatus.Cancelled);
        }
    }

    [SqlServerDomainFact]
    public async Task UnsellableSeatReplacementPreservesPersistedHoldAndCache()
    {
        await using var context = Context();
        var seed = TestDbContextFactory.SeedTrip(context);
        var store = new CommercialHandlerFactory.MemoryStore();
        var sessions = new CheckoutSessionService(store, new DateTimeService(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));
        var hold = await CommercialHandlerFactory.Checkout(context, new SqlCommercialTransactionRunner(context), sessions)
            .HoldAsync(new([new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id)]));
        var cache = store.Items.ToDictionary(p => p.Key, p => p.Value);
        BookingSellabilityTests.Invalidate(context, seed, "unsupportedCurrency");
        await using var replacement = Context();
        await Assert.ThrowsAsync<ConcurrencyException>(() => CommercialHandlerFactory.Checkout(replacement,
            new SqlCommercialTransactionRunner(replacement), sessions).UpdateSeatsAsync(hold.OrderId,
            hold.Bookings.Single().BookingId, new([seed.Seat2.Id], [new("Replacement", null, null)]), hold.AccessToken));
        await using var verify = Context();
        var reservation = await verify.SeatReservations.SingleAsync();
        reservation.SeatId.Should().Be(seed.Seat1.Id);
        reservation.Status.Should().Be(ReservationStatus.Pending);
        (await verify.PurchaseOrders.SingleAsync()).ExpiresAt.Should().Be(hold.ExpiresAt);
        (await verify.PurchaseOrders.SingleAsync()).QuotedTotal.Should().Be(hold.QuotedTotal);
        store.Items.Should().BeEquivalentTo(cache);
        (await verify.Tickets.CountAsync()).Should().Be(0);
    }

    [SqlServerDomainFact]
    public async Task UnsellableReturnLegRollsBackFirstLegAndAccessGrant()
    {
        await using var context = Context();
        var seed = TestDbContextFactory.SeedTrip(context);
        seed.OtherBoat.IsActive = false;
        var returnTrip = new WaterbusSystem.Domain.Entities.Trip { RouteId = seed.Route.Id, BoatId = seed.OtherBoat.Id,
            DepartureTime = DateTimeOffset.UtcNow.AddDays(2), ArrivalTime = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30) };
        foreach (var call in seed.Trip.TripStopCalls)
            returnTrip.TripStopCalls.Add(new WaterbusSystem.Domain.Entities.TripStopCall { TripId = returnTrip.Id,
                RouteStopId = call.RouteStopId, VisitOrder = call.VisitOrder });
        context.Trips.Add(returnTrip);
        await context.SaveChangesAsync();
        var store = new CommercialHandlerFactory.MemoryStore();
        var sessions = new CheckoutSessionService(store, new DateTimeService(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));
        await Assert.ThrowsAsync<WaterbusSystem.Application.Common.Exceptions.ValidationException>(() =>
            CommercialHandlerFactory.Checkout(context, new SqlCommercialTransactionRunner(context), sessions).CreateAsync(
                new("Buyer", "buyer@example.com", null,
                    [new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id),
                     new(returnTrip.Id, [seed.SeatOnOtherBoat.Id], seed.StationA.Id, seed.StationC.Id)])));
        await using var verify = Context();
        (await verify.PurchaseOrders.CountAsync()).Should().Be(0);
        (await verify.Bookings.CountAsync()).Should().Be(0);
        (await verify.SeatReservations.CountAsync()).Should().Be(0);
        (await verify.AccessGrants.CountAsync()).Should().Be(0);
        store.Items.Should().BeEmpty();
    }

    [SqlServerDomainFact]
    public async Task SoftDeletedPaymentResourcesAreDetectedAcrossIndependentContexts()
    {
        await using var seedContext = Context();
        var seed = TestDbContextFactory.SeedTrip(seedContext);
        using var gateway = new VerifiedVnPayFixture();
        foreach (var resource in new[] { "boat", "class" })
        {
            await using var preparation = Context();
            var boat = await preparation.Boats.IgnoreQueryFilters().SingleAsync(b => b.Id == seed.Boat.Id);
            boat.IsDeleted = false;
            await preparation.SaveChangesAsync();
            var runner = new SqlCommercialTransactionRunner(preparation);
            var booking = await CommercialHandlerFactory.Booking(preparation, transactions: runner)
                .CreateAsync(CommercialCheckoutTests.Command(seed));
            await CommercialHandlerFactory.Payment(preparation, gateway.Gateway, runner)
                .CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
            var payment = await preparation.FinancialTransactions.SingleAsync(p => p.OrderId == booking.OrderId);
            await using (var administrator = Context())
            {
                if (resource == "boat")
                    (await administrator.Boats.SingleAsync(b => b.Id == seed.Boat.Id)).IsDeleted = true;
                else
                    (await administrator.SeatClasses.SingleAsync(s => s.Id == seed.StandardClass.Id)).IsDeleted = true;
                await administrator.SaveChangesAsync();
            }
            await using var processing = Context();
            (await CommercialHandlerFactory.Payment(processing, gateway.Gateway, new SqlCommercialTransactionRunner(processing))
                .ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount))).RspCode.Should().Be("00");
            await using var verify = Context();
            var receipt = await verify.FinancialTransactions.SingleAsync(p => p.Id == payment.Id);
            receipt.Status.Should().Be(FinancialTransactionStatus.Succeeded);
            receipt.FailureReason.Should().StartWith("ReconciliationRequired:");
            (await verify.PurchaseOrders.SingleAsync(o => o.Id == booking.OrderId)).Status.Should().Be(OrderStatus.Expired);
            (await verify.SeatReservations.SingleAsync(r => r.BookingId == booking.BookingId)).Status.Should().Be(ReservationStatus.Cancelled);
            (await verify.Tickets.CountAsync()).Should().Be(0);
        }
    }
}
