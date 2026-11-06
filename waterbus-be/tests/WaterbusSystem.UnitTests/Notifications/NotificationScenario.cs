using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Trips.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.TestHelpers;

namespace WaterbusSystem.UnitTests.Notifications;

/// <summary>Dựng luồng thật checkout → thanh toán → IPN (không seed trạng thái bằng tay) để kiểm thử outbox.</summary>
internal static class NotificationScenario
{
    public sealed record Order(Guid OrderId, string Token, FinancialTransaction Payment);

    public static async Task<Order> CheckoutAsync(ApplicationDbContext c, CommercialCheckoutTests.Gateway gateway,
        string name, string email, params CreateCheckoutLegRequest[] legs)
    {
        var checkout = await CommercialHandlerFactory.Checkout(c).CreateAsync(new CreateCheckoutRequest(name, email, null, [.. legs]), default);
        await CommercialHandlerFactory.Payment(c, gateway).CreateVnPayUrlAsync(checkout.OrderId, "127.0.0.1", checkout.AccessToken, default);
        var payment = await c.FinancialTransactions.SingleAsync(t => t.OrderId == checkout.OrderId && t.TransactionType == FinancialTransactionType.Payment);
        return new(checkout.OrderId, checkout.AccessToken!, payment);
    }

    public static Task<WaterbusSystem.Application.DTOs.Responses.Payments.VnPayIpnResultDto> PayAsync(
        ApplicationDbContext c, CommercialCheckoutTests.Gateway gateway, Order order, string status = "00") =>
        CommercialHandlerFactory.Payment(c, gateway).ConfirmIpnAsync(CommercialCheckoutTests.Ipn(order.Payment, status), default);

    public static CreateCheckoutLegRequest Leg(TestDbContextFactory.TripSeedData s, params Seat[] seats) =>
        new(s.Trip.Id, seats.Select(x => x.Id).ToList(), s.StationA.Id, s.StationC.Id,
            seats.Select((x, i) => new CreateCheckoutPassengerRequest("Hanh khach " + (i + 1), null, null)).ToList());

    public static Trip AddReturnTrip(ApplicationDbContext c, TestDbContextFactory.TripSeedData seed)
    {
        var trip = new Trip { Route = seed.Route, RouteId = seed.Route.Id, BoatId = seed.Boat.Id,
            DepartureTime = DateTimeOffset.UtcNow.AddDays(2), ArrivalTime = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30) };
        c.Trips.Add(trip);
        foreach (var call in seed.Trip.TripStopCalls)
            c.TripStopCalls.Add(new TripStopCall { Trip = trip, TripId = trip.Id, RouteStop = call.RouteStop, VisitOrder = call.VisitOrder });
        c.SaveChanges();
        return trip;
    }

    public static TripService TripSvc(ApplicationDbContext c, bool admin = true)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        user.Setup(u => u.UserRole).Returns(admin ? "Admin" : "Captain");
        var clock = new Mock<IDateTimeService>();
        clock.Setup(x => x.Now).Returns(() => DateTimeOffset.UtcNow);
        return new TripService(new TripRepository(c), new IncidentRepository(c), new EfUnitOfWork(c), user.Object, clock.Object,
            Mock.Of<IOperationsAlertPublisher>(), Mock.Of<IUserDirectory>(), TestNotifications.Outbox(c), new DirectCommercialTransactionRunner());
    }

    public static ChangeTripStatusRequest Op(TripOperation op) => new(op, null, null);
}
