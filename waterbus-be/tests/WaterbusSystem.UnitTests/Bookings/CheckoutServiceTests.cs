using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

using AppValidationException =
    WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CheckoutServiceTests
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
    public async Task HoldAsync_ValidRequest_CreatesDraftOrder()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        var request = new CreateCheckoutHoldRequest([
            new CheckoutHoldLegRequest(
                seed.Trip.Id,
                [seed.Seat1.Id],
                seed.StationA.Id,
                seed.StationC.Id)
        ]);

        var result = await CreateService(context).HoldAsync(request);

        Assert.Single(result.Bookings);
        Assert.Equal(15000m, result.QuotedTotal);

        Assert.Equal(
            OrderStatus.Draft,
            context.PurchaseOrders.Single().Status);

        Assert.Equal(
            BookingStatus.Draft,
            context.Bookings.Single().Status);

        Assert.Single(context.SeatReservations);
        Assert.Empty(context.Tickets);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Single(context.AccessGrants);

        Assert.NotEqual(
            result.AccessToken,
            context.AccessGrants.Single().TokenHash);
    }

    [Fact]
    public async Task HoldAsync_InvalidRequest_DoesNotCreateOrder()
    {
        using var context = TestDbContextFactory.CreateDbContext();

        var request = new CreateCheckoutHoldRequest([]);

        await Assert.ThrowsAsync<AppValidationException>(
            () => CreateService(context).HoldAsync(request));

        Assert.Empty(context.PurchaseOrders);
        Assert.Empty(context.Bookings);
    }

    [Fact]
    public async Task HoldEndpoint_UsesCheckoutService()
    {
        var service = new Mock<ICheckoutService>();

        var response = new CheckoutResponse(
            Guid.NewGuid(),
            15000m,
            "VND",
            "Draft",
            DateTimeOffset.UtcNow.AddMinutes(10),
            "test-guest-token",
            []);

        service
            .Setup(s => s.HoldAsync(
                It.IsAny<CreateCheckoutHoldRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new CheckoutsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var request = new CreateCheckoutHoldRequest([]);

        var action = await controller.Hold(request);

        var created = Assert.IsType<CreatedAtActionResult>(action);

        Assert.Equal(nameof(CheckoutsController.Get), created.ActionName);
        Assert.Same(response, created.Value);

        service.Verify(s => s.HoldAsync(
            request,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
