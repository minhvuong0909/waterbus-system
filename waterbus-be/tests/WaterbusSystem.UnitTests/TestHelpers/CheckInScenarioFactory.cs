using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.UnitTests.TestHelpers;

/// <summary>
/// Dựng 1 Ticket đã phát hành (BR-COM-08) kèm cửa sổ check-in và ScannerAssignment, dùng chung cho
/// test SubmitCheckInCommand và SyncCheckInEventsCommand.
/// </summary>
public static class CheckInScenarioFactory
{
    public static (TestDbContextFactory.TripSeedData Seed, Booking Booking, Ticket Ticket, TripStopCall BoardingCall,
        DateTimeOffset Now) SeedCheckInScenario(ApplicationDbContext context, Guid staffId, bool withAssignment = true)
    {
        var seed = TestDbContextFactory.SeedTrip(context);
        var boardingCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var disembarkingCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 3);

        var departure = seed.Trip.DepartureTime;
        var now = departure.AddMinutes(-10);
        boardingCall.CheckInOpenAt = departure.AddMinutes(-30);
        boardingCall.CheckInCloseAt = departure.AddMinutes(-5);

        var order = new PurchaseOrder
        {
            PurchaserName = "Khach Test", PurchaserEmail = "khach@test.com",
            QuotedTotal = TestDbContextFactory.StandardCommuterFare, Status = OrderStatus.Confirmed
        };
        var booking = new Booking
        {
            Order = order, OrderId = order.Id, BookingCode = Guid.NewGuid().ToString("N"),
            CustomerName = order.PurchaserName, CustomerEmail = order.PurchaserEmail,
            QuotedTotal = TestDbContextFactory.StandardCommuterFare, Status = BookingStatus.Confirmed,
            PaidAllocation = TestDbContextFactory.StandardCommuterFare
        };
        booking.SetJourney(boardingCall, disembarkingCall);

        var reservation = new SeatReservation
        {
            TripId = seed.Trip.Id, SeatId = seed.Seat1.Id, BookingId = booking.Id, Booking = booking,
            BoardingStopOrder = boardingCall.VisitOrder, DisembarkingStopOrder = disembarkingCall.VisitOrder,
            Status = ReservationStatus.Confirmed, PassengerName = "Nguyen Van A",
            QuotedFare = TestDbContextFactory.StandardCommuterFare, PaidAllocation = TestDbContextFactory.StandardCommuterFare,
            SeatCodeAtSale = seed.Seat1.SeatCode, SeatClassAtSale = seed.StandardClass.Name
        };
        booking.SeatReservations.Add(reservation);

        var payment = new FinancialTransaction
        {
            OrderId = order.Id, Order = order,
            Status = FinancialTransactionStatus.Succeeded, Amount = TestDbContextFactory.StandardCommuterFare
        };

        var ticket = Ticket.Issue(booking, reservation, payment, TripType.Commuter, DateTimeOffset.UtcNow);
        booking.Tickets.Add(ticket);

        context.PurchaseOrders.Add(order);
        context.Bookings.Add(booking);
        context.SeatReservations.Add(reservation);
        context.FinancialTransactions.Add(payment);
        context.Tickets.Add(ticket);

        if (withAssignment)
        {
            var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
            context.ScannerDevices.Add(device);
            context.ScannerAssignments.Add(new ScannerAssignment
            {
                DeviceId = device.Id, Device = device,
                StaffAccountId = staffId,
                TripStopCallId = boardingCall.Id, TripStopCall = boardingCall,
                IsPrimaryOffline = true,
                ActiveFrom = departure.AddHours(-1), ActiveUntil = departure.AddHours(1)
            });
        }

        context.SaveChanges();

        return (seed, booking, ticket, boardingCall, now);
    }
}
