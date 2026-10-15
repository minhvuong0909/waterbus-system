using FluentAssertions;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using Xunit;

namespace WaterbusSystem.UnitTests.Refunds;

/// <summary>
/// Test cho logic quyết định Ticket nào đủ điều kiện hoàn tiền - trái tim của BR-RF-01 → BR-RF-10.
/// Dựa trực tiếp vào các ví dụ chuẩn trong tài liệu BR (Ví dụ B).
/// </summary>
public class RefundEligibilityEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    private static readonly Guid SharedTripId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TripStopCall Call(int visitOrder, string status = "Planned", DateTimeOffset? actualArrival = null)
    {
        return new TripStopCall
        {
            TripId = SharedTripId,
            VisitOrder = visitOrder,
            Status = status,
            ActualArrivalTime = actualArrival,
            RouteStop = new RouteStop { SequenceNo = visitOrder, CanBoard = true, CanAlight = true }
        };
    }

    private static Booking MakeBooking(TripStopCall boarding, TripStopCall disembarking, BookingStatus status = BookingStatus.Confirmed)
    {
        var booking = new Booking { Status = status, QuotedTotal = 30000m };
        booking.SetJourney(boarding, disembarking);
        return booking;
    }

    private static Ticket MakeTicket(TicketStatus status = TicketStatus.Valid, BoardingStatus boarding = BoardingStatus.NotCheckedIn)
    {
        return new Ticket { Status = status, BoardingStatus = boarding, PaidFareSnapshot = 15000m, FaceFareSnapshot = 15000m };
    }

    [Fact]
    public void Evaluate_WhenTripStillScheduled_ShouldNotBeEligible()
    {
        var trip = new Trip { Status = TripStatus.Scheduled };
        var booking = MakeBooking(Call(1), Call(2));

        var decision = RefundEligibilityEvaluator.Evaluate(MakeTicket(), booking, trip);

        decision.IsEligible.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WhenTicketIsNoShow_ShouldNotBeEligible_BecauseCustomerMissedIt()
    {
        // BR-RF-06: NoShow do khách không đến/không check-in đúng hạn không được hoàn tiền.
        var trip = new Trip { Status = TripStatus.Terminated };
        var booking = MakeBooking(Call(1), Call(3));

        var decision = RefundEligibilityEvaluator.Evaluate(
            MakeTicket(boarding: BoardingStatus.NoShow), booking, trip);

        decision.IsEligible.Should().BeFalse();
        decision.Reason.Should().Contain("NoShow");
    }

    [Fact]
    public void Evaluate_WhenAlreadyServedToDisembarkingStation_ShouldNotBeEligible_BecauseIncidentCameAfter()
    {
        // BR-RF-03: Booking đã được phục vụ đến đúng bến xuống dự kiến trước khi Trip gặp Incident
        // thì coi là đã hoàn tất; không refund vì sự cố xảy ra sau đó.
        var trip = new Trip { Status = TripStatus.Terminated };
        var disembarking = Call(3, status: "Departed", actualArrival: Now.AddHours(-1));
        var booking = MakeBooking(Call(1), disembarking);

        var decision = RefundEligibilityEvaluator.Evaluate(MakeTicket(), booking, trip);

        decision.IsEligible.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WhenTripCancelledBeforeDeparture_ShouldBeFullyEligible()
    {
        // BR-RF-01: Trip bị Cancelled trước khi bắt đầu -> hoàn 100% số tiền đã thanh toán.
        var trip = new Trip { Status = TripStatus.Cancelled };
        var booking = MakeBooking(Call(1), Call(3));

        var decision = RefundEligibilityEvaluator.Evaluate(MakeTicket(), booking, trip);

        decision.IsEligible.Should().BeTrue();
        decision.Reason.Should().Contain("hủy");
    }

    [Fact]
    public void Evaluate_WhenTripTerminatedMidJourneyAndSegmentNotServed_ShouldBeFullyEligible()
    {
        // BR-RF-04: chưa được phục vụ do Trip bị dừng -> hoàn 100%, không phân biệt tỷ lệ.
        var trip = new Trip { Status = TripStatus.Terminated };
        var booking = MakeBooking(Call(2), Call(3));

        var decision = RefundEligibilityEvaluator.Evaluate(MakeTicket(), booking, trip);

        decision.IsEligible.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WhenTripTerminatedAndBoardingStationNeverReached_ShouldStillBeEligible()
    {
        // BR-RF-06: KHÔNG đánh dấu NoShow cho hành khách không có cơ hội boarding vì Trip bị chấm dứng
        // trước khi tới bến lên -> vé vẫn đủ điều kiện hoàn.
        var trip = new Trip { Status = TripStatus.Terminated };
        var booking = MakeBooking(Call(2), Call(3));

        var decision = RefundEligibilityEvaluator.Evaluate(
            MakeTicket(boarding: BoardingStatus.NotCheckedIn), booking, trip);

        decision.IsEligible.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WhenTripCancelsPaidTicket_StillRefundsItButNeverCompletedTickets()
    {
        var trip = new Trip { Status = TripStatus.Cancelled };
        var booking = MakeBooking(Call(1), Call(3));

        RefundEligibilityEvaluator.Evaluate(MakeTicket(TicketStatus.Cancelled), booking, trip)
            .IsEligible.Should().BeTrue();

        RefundEligibilityEvaluator.Evaluate(MakeTicket(TicketStatus.Completed), booking, trip)
            .IsEligible.Should().BeFalse();
    }

    /// <summary>
    /// BR-RF-09: trong CÙNG một Booking nhóm, chuyến bị chấm dứng giữa đường nên đoạn đi chưa được
    /// phục vụ. Vé NoShow của khách bỏ lỡ check-in trước sự cố thì KHÔNG hoàn; các vé còn lại cùng
    /// Booking được hoàn 100% paid allocation, không tính tỷ lệ.
    /// </summary>
    [Fact]
    public void Evaluate_GroupBookingOnInterruptedTrip_RefundsEveryoneExceptNoShow()
    {
        var trip = new Trip { Status = TripStatus.Terminated };
        // Bến xuống (VisitOrder 3) chưa tới được -> cả Booking chưa được phục vụ.
        var booking = MakeBooking(Call(1), Call(3));

        var checkedIn = MakeTicket(boarding: BoardingStatus.CheckedIn);
        var notCheckedIn = MakeTicket(boarding: BoardingStatus.NotCheckedIn);
        var noShow = MakeTicket(boarding: BoardingStatus.NoShow);

        RefundEligibilityEvaluator.Evaluate(checkedIn, booking, trip).IsEligible.Should().BeTrue();
        RefundEligibilityEvaluator.Evaluate(notCheckedIn, booking, trip).IsEligible.Should().BeTrue();
        RefundEligibilityEvaluator.Evaluate(noShow, booking, trip).IsEligible.Should().BeFalse();

        // BR-RF-09: khoản hoàn = tổng paid allocation của các Ticket đủ điều kiện (2 x 15.000),
        // không bao gồm vé NoShow.
        checkedIn.PaidFareSnapshot.Should().Be(15000m);
        notCheckedIn.PaidFareSnapshot.Should().Be(15000m);
    }

    /// <summary>
    /// Ngược lại: nếu chuyến đã phục vụ tới bến xuống dự kiến thì BR-RF-03 áp dụng cho MỌI vé trong
    /// Booking đó, không chỉ khác biệt CheckedIn/NoShow - vì mọi vé cùng Booking đều đi cùng đoạn.
    /// </summary>
    [Fact]
    public void Evaluate_GroupBookingWhoseJourneyWasCompleted_RefundsNobody()
    {
        var trip = new Trip { Status = TripStatus.Terminated };
        var disembarking = Call(3, status: "Departed", actualArrival: Now.AddHours(-1));
        var booking = MakeBooking(Call(1), disembarking);

        RefundEligibilityEvaluator.Evaluate(MakeTicket(boarding: BoardingStatus.CheckedIn), booking, trip)
            .IsEligible.Should().BeFalse();
        RefundEligibilityEvaluator.Evaluate(MakeTicket(boarding: BoardingStatus.NoShow), booking, trip)
            .IsEligible.Should().BeFalse();
    }

    /// <summary>
    /// BR-RF-02/RF-03: chuyến A->B->C->D bị chấm dứng khi đang ở C. Các Booking đã tới đúng bến
    /// xuống thì không hoàn; các đoạn còn lại thì hoàn 100%. Không phát Voucher 20% (BR-RF-05).
    /// </summary>
    [Fact]
    public void Evaluate_TripTerminatedAtStationC_MatchesBusinessRuleExampleB()
    {
        var trip = new Trip { Status = TripStatus.Terminated };

        var callA = Call(1, status: "Departed", actualArrival: Now.AddHours(-3));
        var callB = Call(2, status: "Departed", actualArrival: Now.AddHours(-2));
        var callC = Call(3, status: "Arrived", actualArrival: Now);
        var callD = Call(4);

        // B101: A -> B, đã tới đúng bến xuống B trước Incident -> không refund
        var b101 = MakeBooking(callA, callB);
        RefundEligibilityEvaluator.Evaluate(MakeTicket(boarding: BoardingStatus.CheckedIn), b101, trip)
            .IsEligible.Should().BeFalse();

        // B102: B -> D, chưa tới đích do Trip dừng -> refund 100%
        var b102 = MakeBooking(callB, callD);
        RefundEligibilityEvaluator.Evaluate(MakeTicket(boarding: BoardingStatus.CheckedIn), b102, trip)
            .IsEligible.Should().BeTrue();

        // B103: C -> D, Trip chưa tới được bến C -> refund 100%
        var b103 = MakeBooking(callC, callD);
        RefundEligibilityEvaluator.Evaluate(MakeTicket(boarding: BoardingStatus.NotCheckedIn), b103, trip)
            .IsEligible.Should().BeTrue();

        // B104: khách tự NoShow tại A -> Incident xảy ra sau đó nên không tự refund
        var b104 = MakeBooking(callA, callB);
        RefundEligibilityEvaluator.Evaluate(MakeTicket(boarding: BoardingStatus.NoShow), b104, trip)
            .IsEligible.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShouldSupportSightseeingLoopWhereBoardingAndAlightingShareOneStation()
    {
        // BR-ROUTE-06: chuyến vòng Sightseeing lên và xuống cùng một Station vật lý nhưng là hai
        // lần ghé khác nhau theo VisitOrder. Booking vẫn hợp lệ và vẫn có thể đánh giá hoàn tiền.
        var trip = new Trip { Status = TripStatus.Cancelled, TripType = TripType.Sightseeing };

        var boarding = Call(1);
        var disembarking = Call(3);
        var booking = new Booking { Status = BookingStatus.Confirmed };
        booking.SetJourney(boarding, disembarking);

        var decision = RefundEligibilityEvaluator.Evaluate(MakeTicket(), booking, trip);

        decision.IsEligible.Should().BeTrue();
    }
}