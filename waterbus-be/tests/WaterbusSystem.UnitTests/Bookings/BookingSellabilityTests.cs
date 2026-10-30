using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.UnitTests.Payments;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class BookingSellabilityTests
{
    public static TheoryData<string> InvalidInventory => new()
    {
        "inactiveBoat", "deletedBoat", "inactiveClass", "deletedClass", "inactiveSeat", "deletedSeat",
        "unsupportedCurrency", "negativeFare", "inactiveFare", "expiredFare", "futureFare", "duplicateFare"
    };

    public static TheoryData<string> WithdrawnInventory => new()
    {
        "inactiveBoat", "deletedBoat", "inactiveClass", "deletedClass", "inactiveSeat", "deletedSeat"
    };

    internal static void Invalidate(ApplicationDbContext context, TestDbContextFactory.TripSeedData seed, string issue)
    {
        var fare = context.FareRules.Single(f => f.TripType == nameof(TripType.Commuter) && f.SeatClassId == seed.StandardClass.Id);
        switch (issue)
        {
            case "inactiveBoat": seed.Boat.IsActive = false; break;
            case "deletedBoat": seed.Boat.IsDeleted = true; break;
            case "inactiveClass": seed.StandardClass.IsActive = false; break;
            case "deletedClass": seed.StandardClass.IsDeleted = true; break;
            case "inactiveSeat": seed.Seat1.IsActive = seed.Seat2.IsActive = false; break;
            case "deletedSeat": seed.Seat1.IsDeleted = seed.Seat2.IsDeleted = true; break;
            case "unsupportedCurrency": fare.Currency = "USD"; break;
            case "negativeFare": fare.Price = -1; break;
            case "inactiveFare": fare.IsActive = false; break;
            case "expiredFare": fare.EffectiveTo = DateTimeOffset.UtcNow.AddSeconds(-1); break;
            case "futureFare": fare.EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1); break;
            case "duplicateFare":
                context.FareRules.Add(new FareRule { TripType = fare.TripType, SeatClassId = fare.SeatClassId,
                    Price = fare.Price, Currency = fare.Currency, EffectiveFrom = DateTimeOffset.UtcNow.AddHours(-1), IsActive = true });
                break;
            default: throw new ArgumentException("Unknown inventory case.", nameof(issue));
        }
        context.SaveChanges();
    }

    [Theory]
    [MemberData(nameof(InvalidInventory))]
    public async Task InvalidInventoryCannotCreateABookingOrPartialHold(string issue)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        Invalidate(context, seed, issue);
        var error = await Record.ExceptionAsync(() => CommercialHandlerFactory.Booking(context)
            .CreateAsync(CommercialCheckoutTests.Command(seed)));
        Assert.NotNull(error);
        Assert.True(error is ValidationException or NotFoundException, error.ToString());
        Assert.Empty(context.PurchaseOrders);
        Assert.Empty(context.Bookings);
        Assert.Empty(context.SeatReservations);
        Assert.Empty(context.AccessGrants);
        Assert.Empty(context.Tickets);
    }

    [Theory]
    [MemberData(nameof(InvalidInventory))]
    public async Task RejectedSeatReplacementPreservesOriginalReservationDeadlineAndCache(string issue)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var store = new CommercialHandlerFactory.MemoryStore();
        var sessions = new CheckoutSessionService(store, new DateTimeService(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));
        var checkout = CommercialHandlerFactory.Checkout(context, sessions: sessions);
        var hold = await checkout.HoldAsync(new([new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id)]));
        var cache = store.Items.ToDictionary(p => p.Key, p => p.Value);
        Invalidate(context, seed, issue);
        var error = await Record.ExceptionAsync(() => checkout.UpdateSeatsAsync(hold.OrderId, hold.Bookings.Single().BookingId,
            new([seed.Seat2.Id], [new("Replacement", null, null)]), hold.AccessToken));
        Assert.NotNull(error);
        Assert.True(error is ConcurrencyException or NotFoundException, error.ToString());
        var reservation = Assert.Single(context.SeatReservations);
        Assert.Equal(seed.Seat1.Id, reservation.SeatId);
        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.Equal(hold.ExpiresAt, context.PurchaseOrders.Single().ExpiresAt);
        Assert.Equal(hold.QuotedTotal, context.PurchaseOrders.Single().QuotedTotal);
        Assert.Equal(cache, store.Items);
        Assert.Empty(context.Tickets);
    }

    [Fact]
    public async Task ValidVndAdjacentSegmentsRemainIndependentlyBookable()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var service = CommercialHandlerFactory.Booking(context);
        var first = await service.CreateAsync(CommercialCheckoutTests.Command(seed) with { DisembarkingStationId = seed.StationB.Id });
        var second = await service.CreateAsync(CommercialCheckoutTests.Command(seed) with { BoardingStationId = seed.StationB.Id });
        Assert.Equal(TestDbContextFactory.StandardCommuterFare, first.TotalAmount);
        Assert.Equal(TestDbContextFactory.StandardCommuterFare, second.TotalAmount);
        Assert.Equal(2, context.SeatReservations.Count());
        Assert.All(context.SeatReservations, r => Assert.Equal(seed.Seat1.Id, r.SeatId));
        Assert.All(context.PurchaseOrders, o => Assert.Equal("VND", o.Currency));
        Assert.Empty(context.Tickets);
    }

    [Theory]
    [MemberData(nameof(WithdrawnInventory))]
    public async Task PaymentInitiationRejectsInventoryWithdrawnAfterHold(string issue)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await CommercialHandlerFactory.Booking(context).CreateAsync(CommercialCheckoutTests.Command(seed));
        Invalidate(context, seed, issue);
        using var gateway = new VerifiedVnPayFixture();
        await Assert.ThrowsAsync<ConcurrencyException>(() => CommercialHandlerFactory.Payment(context, gateway.Gateway)
            .CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken));
        Assert.Empty(context.FinancialTransactions);
        Assert.Empty(context.Tickets);
    }

    [Theory]
    [MemberData(nameof(WithdrawnInventory))]
    public async Task VerifiedPaymentForWithdrawnInventoryIsRecordedWithoutIssuingTickets(string issue)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await CommercialHandlerFactory.Booking(context).CreateAsync(CommercialCheckoutTests.Command(seed));
        using var gateway = new VerifiedVnPayFixture();
        var payments = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        await payments.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var payment = context.FinancialTransactions.Single();
        Invalidate(context, seed, issue);
        Assert.Equal("00", (await payments.ConfirmIpnAsync(gateway.Callback(payment.MerchantReference!, payment.Amount))).RspCode);
        Assert.Equal(FinancialTransactionStatus.Succeeded, payment.Status);
        Assert.StartsWith("ReconciliationRequired:", payment.FailureReason);
        Assert.Equal(OrderStatus.Expired, context.PurchaseOrders.Single().Status);
        Assert.All(context.SeatReservations, r => Assert.Equal(ReservationStatus.Cancelled, r.Status));
        Assert.Empty(context.Tickets);
    }

    [Fact]
    public async Task ExistingVndQuoteSurvivesFareChangesAndIssuedTicketsAreNotRetroactivelyInvalidated()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await CommercialHandlerFactory.Booking(context).CreateAsync(CommercialCheckoutTests.Command(seed));
        using var gateway = new VerifiedVnPayFixture();
        var payments = CommercialHandlerFactory.Payment(context, gateway.Gateway);
        await payments.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var payment = context.FinancialTransactions.Single();
        var fare = context.FareRules.Single(f => f.TripType == nameof(TripType.Commuter) && f.SeatClassId == seed.StandardClass.Id);
        fare.Price *= 2;
        fare.Currency = "USD";
        fare.IsActive = false;
        await context.SaveChangesAsync();
        var callback = gateway.Callback(payment.MerchantReference!, payment.Amount);
        Assert.Equal("00", (await payments.ConfirmIpnAsync(callback)).RspCode);
        Assert.Equal(TestDbContextFactory.StandardCommuterFare, context.Tickets.Single().PaidFareSnapshot);
        seed.Boat.IsActive = seed.StandardClass.IsActive = false;
        await context.SaveChangesAsync();
        Assert.Equal("02", (await payments.ConfirmIpnAsync(callback)).RspCode);
        Assert.Equal(TicketStatus.Valid, context.Tickets.Single().Status);
        Assert.Equal(OrderStatus.Confirmed, context.PurchaseOrders.Single().Status);
    }
}
