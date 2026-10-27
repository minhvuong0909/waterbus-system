
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CheckoutSubmitFlowTests
{
    private static CheckoutService CreateService(
        ApplicationDbContext context)
    {
        return new CheckoutService(
            new OrderRepository(context),
            CommercialHandlerFactory.Creation(context),
            new DirectCommercialTransactionRunner(),
            CommercialHandlerFactory.Effects(context),
            new CheckoutAccessService(
                context, new CheckoutOptions()),
            new EfUnitOfWork(context),
            new SubmitCheckoutRequestValidator(),
            new CreateCheckoutHoldRequestValidator(),
            new CreateCheckoutRequestValidator(),
            CommercialHandlerFactory.SeatChanges(context));
    }

    [Fact]
    public async Task HoldThenSubmit_ValidRequest_ShouldPersistPendingPayment()
    {
        await using var context =
            TestDbContextFactory.CreateDbContext();

        var seed = TestDbContextFactory.SeedTrip(context);
        var service = CreateService(context);

        var checkout = await service.HoldAsync(
            new CreateCheckoutHoldRequest([
                new CheckoutHoldLegRequest(
                    seed.Trip.Id,
                    [seed.Seat1.Id],
                    seed.StationA.Id,
                    seed.StationC.Id)
            ]));

        checkout.Status.Should().Be(nameof(OrderStatus.Draft));
        checkout.Bookings.Should().ContainSingle();
        checkout.AccessToken.Should().NotBeNullOrWhiteSpace();

        var bookingId = checkout.Bookings.Single().BookingId;

        var submitted = await service.SubmitAsync(
            checkout.OrderId,
            new SubmitCheckoutRequest(
                "Nguyen An",
                "an@example.com",
                "0912345678",
                [
                    new CheckoutPassengerRequest(
                        bookingId,
                        seed.Seat1.Id,
                        "Tran Binh",
                        "binh@example.com",
                        null)
                ]),
            checkout.AccessToken);

        submitted.Should().BeTrue();

        var order = await context.PurchaseOrders
            .Include(o => o.Bookings)
                .ThenInclude(b => b.SeatReservations)
            .SingleAsync(o => o.Id == checkout.OrderId);

        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.PurchaserName.Should().Be("Nguyen An");
        order.PurchaserEmail.Should().Be("an@example.com");

        var booking = order.Bookings.Single();

        booking.Status.Should().Be(
            BookingStatus.PendingPayment);

        var reservation = booking.SeatReservations.Single();

        reservation.PassengerName.Should().Be("Tran Binh");
        reservation.PassengerEmail.Should().Be(
            "binh@example.com");

        (await context.Tickets.CountAsync()).Should().Be(0);
    }


    [Fact]
    public async Task HoldThenUpdateSeats_ShouldReplaceSeatAndPreserveExpiry()
    {
        await using var context =
            TestDbContextFactory.CreateDbContext();

        var seed = TestDbContextFactory.SeedTrip(context);
        var service = CreateService(context);

        var checkout = await service.HoldAsync(
            new CreateCheckoutHoldRequest([
                new CheckoutHoldLegRequest(
                seed.Trip.Id,
                [seed.Seat1.Id],
                seed.StationA.Id,
                seed.StationC.Id)
            ]));

        var bookingId = checkout.Bookings.Single().BookingId;

        var originalExpiry = (await context.PurchaseOrders
            .SingleAsync(o => o.Id == checkout.OrderId))
            .ExpiresAt;

        var total = await service.UpdateSeatsAsync(
            checkout.OrderId,
            bookingId,
            new UpdateCheckoutSeatsRequest(
                [seed.Seat2.Id],
                [
                    new UpdateCheckoutPassengerRequest(
                    "Tran Binh",
                    "binh@example.com",
                    null)
                ]),
            checkout.AccessToken);

        total.Should().Be(
            TestDbContextFactory.StandardCommuterFare);

        context.ChangeTracker.Clear();

        var order = await context.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Bookings)
                .ThenInclude(b => b.SeatReservations)
            .SingleAsync(o => o.Id == checkout.OrderId);

        order.ExpiresAt.Should().Be(originalExpiry);

        order.QuotedTotal.Should().Be(
            TestDbContextFactory.StandardCommuterFare);

        var reservations = order.Bookings
            .Single()
            .SeatReservations
            .ToList();

        reservations.Should().ContainSingle(r =>
            r.SeatId == seed.Seat1.Id &&
            r.Status == ReservationStatus.Cancelled);

        reservations.Should().ContainSingle(r =>
            r.SeatId == seed.Seat2.Id &&
            r.Status == ReservationStatus.Pending &&
            r.PassengerName == "Tran Binh");

        (await context.Tickets.CountAsync()).Should().Be(0);
    }


    [Fact]
    public async Task Submit_InvalidGuestToken_ShouldRejectAndKeepDraft()
    {
        await using var context =
            TestDbContextFactory.CreateDbContext();

        var seed = TestDbContextFactory.SeedTrip(context);
        var service = CreateService(context);

        var checkout = await service.HoldAsync(
            new CreateCheckoutHoldRequest([
                new CheckoutHoldLegRequest(
                    seed.Trip.Id,
                    [seed.Seat1.Id],
                    seed.StationA.Id,
                    seed.StationC.Id)
            ]));

        var request = new SubmitCheckoutRequest(
            "Nguyen An",
            "an@example.com",
            null);

        Func<Task> act = () => service.SubmitAsync(
            checkout.OrderId,
            request,
            "invalid-guest-token");

        await act.Should().ThrowAsync<ForbiddenAccessException>();

        var order = await context.PurchaseOrders
            .SingleAsync(o => o.Id == checkout.OrderId);

        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task Submit_WrongPassengerSeat_ShouldRejectAndKeepDraft()
    {
        await using var context =
            TestDbContextFactory.CreateDbContext();

        var seed = TestDbContextFactory.SeedTrip(context);
        var service = CreateService(context);

        var checkout = await service.HoldAsync(
            new CreateCheckoutHoldRequest([
                new CheckoutHoldLegRequest(
                    seed.Trip.Id,
                    [seed.Seat1.Id],
                    seed.StationA.Id,
                    seed.StationC.Id)
            ]));

        var bookingId = checkout.Bookings.Single().BookingId;

        var request = new SubmitCheckoutRequest(
            "Nguyen An",
            "an@example.com",
            null,
            [
                new CheckoutPassengerRequest(
                    bookingId,
                    seed.Seat2.Id, // Sai ghế
                    "Tran Binh",
                    null,
                    null)
            ]);

        Func<Task> act = () => service.SubmitAsync(
            checkout.OrderId,
            request,
            checkout.AccessToken);

        await act.Should().ThrowAsync<ConcurrencyException>();

        var order = await context.PurchaseOrders
            .SingleAsync(o => o.Id == checkout.OrderId);

        order.Status.Should().Be(OrderStatus.Draft);
    }


    [Fact]
    public async Task HoldThenCancel_ShouldReleaseSeatsAndCancelOrder()
    {
        await using var context =
            TestDbContextFactory.CreateDbContext();

        var seed = TestDbContextFactory.SeedTrip(context);
        var service = CreateService(context);

        var checkout = await service.HoldAsync(
            new CreateCheckoutHoldRequest([
                new CheckoutHoldLegRequest(
                seed.Trip.Id,
                [seed.Seat1.Id],
                seed.StationA.Id,
                seed.StationC.Id)
            ]));

        var cancelled = await service.CancelAsync(
            checkout.OrderId,
            checkout.AccessToken);

        cancelled.Should().BeTrue();

        var order = await context.PurchaseOrders
            .Include(o => o.Bookings)
            .ThenInclude(b => b.SeatReservations)
            .SingleAsync(o => o.Id == checkout.OrderId);

        order.Status.Should().Be(OrderStatus.Cancelled);

        order.Bookings.Should().OnlyContain(
            b => b.Status == BookingStatus.Cancelled);

        order.Bookings
            .SelectMany(b => b.SeatReservations)
            .Should().OnlyContain(
                r => r.Status == ReservationStatus.Cancelled);
    }
}
