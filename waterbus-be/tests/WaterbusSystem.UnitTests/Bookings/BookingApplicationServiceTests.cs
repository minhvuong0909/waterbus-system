using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Validators.Bookings;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class BookingApplicationServiceTests
{
    internal static BookingService Service(ApplicationDbContext context) => new(
        CommercialHandlerFactory.Creation(context), new DirectCommercialTransactionRunner(),
        CommercialHandlerFactory.Effects(context), new CreateBookingRequestValidator());

    [Fact]
    public async Task BookingEndpointCreatesPendingReservationsWithoutTickets()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var controller = new BookingsController(Service(context))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var action = await controller.CreateBooking(new CreateBookingRequest(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(ReservationStatus.Pending, context.SeatReservations.Single().Status);
        Assert.Empty(context.Tickets);
    }

    [Fact]
    public async Task InvalidBookingIsRejectedBeforePersistence()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        await Assert.ThrowsAsync<ValidationException>(() => Service(context).CreateAsync(new(
            Guid.Empty, [], Guid.Empty, Guid.Empty, "", "invalid", null)));
        Assert.Empty(context.PurchaseOrders);
        Assert.Empty(context.AccessGrants);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("other-order")]
    public async Task CheckoutReadRejectsUnauthorizedTokens(string scenario)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var grant = context.AccessGrants.Single();
        if (scenario == "expired") grant.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        if (scenario == "revoked") grant.RevokedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        var access = new CheckoutAccessService(new AccessGrantRepository(context), new CheckoutOptions());
        var reads = new CheckoutReadService(new OrderRepository(context), access);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => reads.GetAsync(
            scenario == "other-order" ? Guid.NewGuid() : booking.OrderId,
            scenario == "missing" ? null : booking.AccessToken));
    }

    [Fact]
    public async Task ValidTokenReadsOrderAndResolvesGuestIdentity()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var access = new CheckoutAccessService(new AccessGrantRepository(context), new CheckoutOptions());
        var detail = await new CheckoutReadService(new OrderRepository(context), access)
            .GetAsync(booking.OrderId, booking.AccessToken);
        Assert.Equal(booking.OrderId, detail.OrderId);
        Assert.Equal(booking.BookingId, Assert.Single(detail.Bookings).BookingId);
        Assert.Empty(detail.Payments);
        var identity = await access.FindIdentityAsync(booking.AccessToken, default);
        Assert.Equal(booking.OrderId, identity!.OrderId);
        Assert.Null(identity.BookingId);
    }

    [Fact]
    public async Task ExpirationIsIdempotentAndLeavesConfirmedOrdersAlone()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var order = context.PurchaseOrders.Single();
        order.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();
        var service = new CheckoutExpirationService(new OrderRepository(context), new EfUnitOfWork(context),
            new DirectCommercialTransactionRunner(), new CheckoutOptions());
        Assert.Equal(1, await service.ExpireAsync());
        Assert.Equal(0, await service.ExpireAsync());
        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.Equal(ReservationStatus.Cancelled, context.SeatReservations.Single().Status);
        order.Status = OrderStatus.Confirmed;
        await context.SaveChangesAsync();
        Assert.Equal(0, await service.ExpireAsync());
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }
}
