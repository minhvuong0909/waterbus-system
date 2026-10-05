using FluentAssertions;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using Xunit;

namespace WaterbusSystem.UnitTests.Domain;

public class CommercialDomainTests
{
    [Theory]
    [InlineData(FinancialTransactionStatus.Requested, false)]
    [InlineData(FinancialTransactionStatus.Failed, false)]
    [InlineData(FinancialTransactionStatus.Succeeded, true)]
    public void Ticket_IssuesOnlyAfterVerifiedPaymentWithFareSnapshots(FinancialTransactionStatus status, bool valid)
    {
        var booking = new Booking
        { OrderId = Guid.NewGuid(), TripId = Guid.NewGuid(), Status = BookingStatus.Confirmed, PaidAllocation = 15000m };
        var reservation = new SeatReservation
        {
            BookingId = booking.Id, TripId = booking.TripId, PassengerName = "Passenger",
            SeatCodeAtSale = "A1", SeatClassAtSale = "Standard", QuotedFare = 15000m,
            PaidAllocation = 15000m, Status = ReservationStatus.Confirmed
        };
        var payment = new FinancialTransaction { OrderId = booking.OrderId, Status = status, Amount = 15000m };
        var issuedAt = DateTimeOffset.UtcNow;
        var act = () => Ticket.Issue(booking, reservation, payment, TripType.Commuter, issuedAt);
        if (!valid) { act.Should().Throw<InvalidOperationException>(); return; }
        var ticket = act();
        ticket.Status.Should().Be(TicketStatus.Valid);
        ticket.BoardingStatus.Should().Be(BoardingStatus.NotCheckedIn);
        ticket.PaidFareSnapshot.Should().Be(15000m);
        ticket.FareSeatClassSnapshot.Should().Be("Standard");
        ticket.IssuedAt.Should().Be(issuedAt);
        act.Should().Throw<InvalidOperationException>("a reservation cannot issue a second Ticket");
    }

    [Fact]
    public void Journey_AllowsTwoVisitsToTheSameSightseeingStation()
    {
        var tripId = Guid.NewGuid();
        var routeStop = new RouteStop { StationId = Guid.NewGuid() };
        var boarding = new TripStopCall { TripId = tripId, RouteStop = routeStop, VisitOrder = 1 };
        var disembarking = new TripStopCall { TripId = tripId, RouteStop = routeStop, VisitOrder = 5 };
        var booking = new Booking();

        booking.SetJourney(boarding, disembarking);

        booking.TripId.Should().Be(tripId);
        booking.BoardingCallId.Should().Be(boarding.Id);
        booking.DisembarkingCallId.Should().Be(disembarking.Id);
    }

    [Theory]
    [InlineData(true, 2, 3)]
    [InlineData(false, 3, 2)]
    [InlineData(false, 2, 2)]
    public void Journey_RejectsCrossTripOrInvalidVisitOrder(bool crossTrip, int from, int to)
    {
        var tripId = Guid.NewGuid();
        var booking = new Booking();
        var act = () => booking.SetJourney(
            new TripStopCall { TripId = tripId, VisitOrder = from },
            new TripStopCall { TripId = crossTrip ? Guid.NewGuid() : tripId, VisitOrder = to });

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(PurchaseMode.OneWay, 1, true)]
    [InlineData(PurchaseMode.OneWay, 2, false)]
    [InlineData(PurchaseMode.RoundTrip, 1, false)]
    [InlineData(PurchaseMode.RoundTrip, 2, true)]
    public void Checkout_RequiresCorrectBookingCount(PurchaseMode mode, int count, bool valid)
    {
        var order = new PurchaseOrder
        {
            PurchaseMode = mode, PurchaserName = "Purchaser", PurchaserEmail = "p@example.com",
            QuotedTotal = count * 15000m
        };
        for (var i = 0; i < count; i++)
            order.Bookings.Add(new Booking { OrderId = order.Id, QuotedTotal = 15000m });

        var act = () => order.ValidateForCheckout();
        if (valid) act.Should().NotThrow();
        else act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Checkout_RejectsAnOrderTotalDifferentFromItsBookings()
    {
        var order = new PurchaseOrder { PurchaserName = "P", PurchaserEmail = "p@example.com", QuotedTotal = 1m };
        order.Bookings.Add(new Booking { OrderId = order.Id, QuotedTotal = 15000m });
        var act = () => order.ValidateForCheckout();
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false, false, 500000, true)]
    [InlineData(true, false, 500000, false)]
    [InlineData(false, true, 500000, false)]
    [InlineData(false, false, 100000, false)]
    public void Refund_RequiresSuccessfulSameOrderPaymentAndRemainingBalance(
        bool wrongOrder, bool failedPayment, decimal balance, bool valid)
    {
        var booking = new Booking { OrderId = Guid.NewGuid() };
        var payment = new FinancialTransaction
        {
            OrderId = wrongOrder ? Guid.NewGuid() : booking.OrderId, Amount = 500000m,
            Status = failedPayment ? FinancialTransactionStatus.Failed : FinancialTransactionStatus.Succeeded
        };
        var refund = new FinancialTransaction
        {
            OrderId = booking.OrderId, OriginalPaymentId = payment.Id, RefundBookingId = booking.Id,
            TransactionType = FinancialTransactionType.Refund, Amount = 200000m
        };
        var ticket = new Ticket { BookingId = booking.Id, PaidFareSnapshot = 200000m };
        refund.RefundAllocations.Add(new RefundTicketAllocation
        {
            RefundTransactionId = refund.Id, TicketId = ticket.Id, Ticket = ticket, Amount = 200000m
        });
        var act = () => refund.ValidateRefund(payment, booking, balance);
        if (valid) act.Should().NotThrow();
        else act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Refund_RejectsCompletedJourneysEvenIfTheRestOfTheTripWasInterrupted()
    {
        var booking = new Booking { OrderId = Guid.NewGuid(), Status = BookingStatus.Completed };
        var payment = new FinancialTransaction
        { OrderId = booking.OrderId, Amount = 15000m, Status = FinancialTransactionStatus.Succeeded };
        var ticket = new Ticket { BookingId = booking.Id, PaidFareSnapshot = 15000m };
        var refund = new FinancialTransaction
        {
            OrderId = booking.OrderId, RefundBookingId = booking.Id, OriginalPaymentId = payment.Id,
            TransactionType = FinancialTransactionType.Refund, Amount = 15000m
        };
        refund.RefundAllocations.Add(new RefundTicketAllocation
        { RefundTransactionId = refund.Id, TicketId = ticket.Id, Ticket = ticket, Amount = 15000m });
        var act = () => refund.ValidateRefund(payment, booking, 15000m);
        act.Should().Throw<InvalidOperationException>();
        booking.Status = BookingStatus.Terminated;
        ticket.Status = TicketStatus.Completed;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Refund_ExcludesNoShowAndNeverProratesAnEligibleTicket()
    {
        var booking = new Booking { OrderId = Guid.NewGuid() };
        var payment = new FinancialTransaction
        { OrderId = booking.OrderId, Amount = 200000m, Status = FinancialTransactionStatus.Succeeded };
        var ticket = new Ticket
        { BookingId = booking.Id, BoardingStatus = BoardingStatus.NoShow, PaidFareSnapshot = 200000m };
        var refund = new FinancialTransaction
        {
            OrderId = booking.OrderId, RefundBookingId = booking.Id, OriginalPaymentId = payment.Id,
            TransactionType = FinancialTransactionType.Refund, Amount = 200000m
        };
        var allocation = new RefundTicketAllocation
        { RefundTransactionId = refund.Id, TicketId = ticket.Id, Ticket = ticket, Amount = 200000m };
        refund.RefundAllocations.Add(allocation);
        var act = () => refund.ValidateRefund(payment, booking, 200000m);
        act.Should().Throw<InvalidOperationException>();

        ticket.BoardingStatus = BoardingStatus.CheckedIn;
        allocation.Amount = refund.Amount = 100000m;
        act.Should().Throw<InvalidOperationException>();
    }
}
