using FluentAssertions;
using FluentValidation.Results;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.Refunds;

/// <summary>Logic chọn Booking cần hoàn và xử lý lô (không cần SQL): điều kiện BR-RF, bỏ qua từng Booking lỗi, phân trang.</summary>
public sealed class RefundBatchServiceTests
{
    private static Ticket NewTicket(string code, decimal paid, BoardingStatus boarding = BoardingStatus.NotCheckedIn) =>
        new() { TicketCode = code, PaidFareSnapshot = paid, Status = TicketStatus.Valid, BoardingStatus = boarding };

    private static Booking NewBooking(string code, params Ticket[] tickets)
    {
        var booking = new Booking
        {
            BookingCode = code,
            Order = new PurchaseOrder { PurchaserName = "Khách " + code, PurchaserEmail = code + "@example.com", Currency = "VND" }
        };
        booking.OrderId = booking.Order.Id;
        foreach (var t in tickets) { t.BookingId = booking.Id; booking.Tickets.Add(t); }
        return booking;
    }

    private sealed class FakeCandidates(Trip trip, IReadOnlyList<Booking> bookings, params Guid[] allocated) : IRefundCandidateRepository
    {
        public Task<Trip> GetTripAsync(Guid tripId, CancellationToken ct) =>
            tripId == trip.Id ? Task.FromResult(trip) : throw new NotFoundException(nameof(Trip), tripId);
        public Task<IReadOnlyList<Booking>> ListBookingsWithTicketsAsync(Guid tripId, CancellationToken ct) => Task.FromResult(bookings);
        public Task<IReadOnlySet<Guid>> GetAllocatedTicketIdsAsync(Guid tripId, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<Guid>>(allocated.ToHashSet());
    }

    private sealed class FakeRequests(Func<RequestRefundRequest, RequestRefundResultDto> handler) : IRefundRequestService
    {
        public List<RequestRefundRequest> Calls { get; } = [];
        public Task<RequestRefundResultDto> RequestAsync(RequestRefundRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add(request);
            return Task.FromResult(handler(request));
        }
    }

    private static RequestRefundResultDto Result(RequestRefundRequest r) =>
        new(Guid.NewGuid(), Guid.NewGuid(), r.BookingId, 10m, "VND", "Processing", "Manual", true, []);

    private static Trip NewTrip(TripStatus status) => new() { Status = status };

    [Fact]
    public async Task Candidates_ListsEligibleBookingsWithPaidAllocationAndPurchaser()
    {
        var trip = NewTrip(TripStatus.Cancelled);
        var booking = NewBooking("BK1", NewTicket("T1", 15000m), NewTicket("T2", 25000m));
        var service = new RefundBatchService(new FakeCandidates(trip, [booking]), new FakeRequests(Result));

        var page = await service.ListCandidatesAsync(new(trip.Id));

        var item = page.Items.Single();
        item.BookingCode.Should().Be("BK1");
        item.Amount.Should().Be(40000m);
        item.TicketCodes.Should().BeEquivalentTo(["T1", "T2"]);
        item.PurchaserEmail.Should().Be("BK1@example.com");
        item.Reason.Should().Contain("hủy");
    }

    [Fact]
    public async Task Candidates_ExcludeNoShowTickets_AlreadyAllocatedTickets_AndBookingsLeftWithNothing()
    {
        var trip = NewTrip(TripStatus.Cancelled);
        var noShow = NewTicket("NS", 10000m, BoardingStatus.NoShow);
        var good = NewTicket("OK", 20000m);
        var refunded = NewTicket("DONE", 30000m);
        var mixed = NewBooking("MIX", noShow, good);                 // BR-RF-09: chỉ vé OK được hoàn
        var onlyNoShow = NewBooking("NSONLY", NewTicket("NS2", 5000m, BoardingStatus.NoShow));
        var alreadyRefunded = NewBooking("REF", refunded);
        var service = new RefundBatchService(new FakeCandidates(trip, [mixed, onlyNoShow, alreadyRefunded], refunded.Id), new FakeRequests(Result));

        var page = await service.ListCandidatesAsync(new(trip.Id));

        var item = page.Items.Single();
        item.BookingCode.Should().Be("MIX");
        item.Amount.Should().Be(20000m);
        item.TicketCodes.Should().Equal("OK");
    }

    [Theory]
    [InlineData(TripStatus.Scheduled)]
    [InlineData(TripStatus.EnRoute)]
    [InlineData(TripStatus.Suspended)]
    public async Task TripNotCancelledOrTerminated_IsConflict_ForBothListAndBatch(TripStatus status)
    {
        var trip = NewTrip(status);
        var requests = new FakeRequests(Result);
        var service = new RefundBatchService(new FakeCandidates(trip, [NewBooking("B", NewTicket("T", 1m))]), requests);

        await service.Invoking(s => s.ListCandidatesAsync(new(trip.Id))).Should().ThrowAsync<ConflictException>();
        await service.Invoking(s => s.RequestForTripAsync(new(trip.Id))).Should().ThrowAsync<ConflictException>();
        requests.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownTrip_IsNotFound() =>
        await new RefundBatchService(new FakeCandidates(NewTrip(TripStatus.Cancelled), []), new FakeRequests(Result))
            .Invoking(s => s.ListCandidatesAsync(new(Guid.NewGuid()))).Should().ThrowAsync<NotFoundException>();

    [Fact]
    public async Task Batch_RequestsEveryCandidate_PassingReasonAndContext()
    {
        var trip = NewTrip(TripStatus.Terminated);
        var requests = new FakeRequests(Result);
        var service = new RefundBatchService(new FakeCandidates(trip,
            [NewBooking("A", NewTicket("A1", 1m)), NewBooking("B", NewTicket("B1", 2m))]), requests);

        var result = await service.RequestForTripAsync(new(trip.Id, "  Bão  ", "10.0.0.1", "admin@waterbus.vn"));

        result.Requested.Should().Be(2);
        result.Skipped.Should().Be(0);
        requests.Calls.Select(c => c.Reason).Should().AllBe("Bão");
        requests.Calls.Should().OnlyContain(c => c.IpAddress == "10.0.0.1" && c.CreatedBy == "admin@waterbus.vn");
    }

    [Fact]
    public async Task Batch_UsesADefaultReason_WhenNoneGiven()
    {
        var trip = NewTrip(TripStatus.Cancelled);
        var requests = new FakeRequests(Result);
        await new RefundBatchService(new FakeCandidates(trip, [NewBooking("A", NewTicket("A1", 1m))]), requests)
            .RequestForTripAsync(new(trip.Id, "  "));
        requests.Calls.Single().Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Batch_ReportsABookingThatFailsAsSkipped_AndStillProcessesTheRest()
    {
        var trip = NewTrip(TripStatus.Cancelled);
        var bad = NewBooking("BAD", NewTicket("B1", 1m));
        var conflict = NewBooking("CONFLICT", NewTicket("C1", 1m));
        var good = NewBooking("GOOD", NewTicket("G1", 1m));
        var requests = new FakeRequests(r =>
        {
            if (r.BookingId == bad.Id)
                throw new ValidationException([new ValidationFailure("BookingId", "Chưa có giao dịch thanh toán thành công.")]);
            if (r.BookingId == conflict.Id) throw new ConcurrencyException("Các vé này đã có yêu cầu hoàn tiền trước đó.");
            return Result(r);
        });
        var service = new RefundBatchService(new FakeCandidates(trip, [bad, conflict, good]), requests);

        var result = await service.RequestForTripAsync(new(trip.Id));

        result.Requested.Should().Be(1);
        result.Created.Single().BookingCode.Should().Be("GOOD");
        result.Skipped.Should().Be(2);
        result.SkippedItems.Single(s => s.BookingCode == "BAD").Reason.Should().Contain("thanh toán");
        result.SkippedItems.Single(s => s.BookingCode == "CONFLICT").Reason.Should().Contain("trước đó");
    }

    [Fact]
    public async Task Batch_DoesNotSwallowUnexpectedErrors()
    {
        var trip = NewTrip(TripStatus.Cancelled);
        var service = new RefundBatchService(new FakeCandidates(trip, [NewBooking("A", NewTicket("A1", 1m))]),
            new FakeRequests(_ => throw new InvalidOperationException("bug")));
        await service.Invoking(s => s.RequestForTripAsync(new(trip.Id))).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Candidates_ArePaged_WithTotalCountOfAllCandidates()
    {
        var trip = NewTrip(TripStatus.Cancelled);
        var bookings = Enumerable.Range(1, 5).Select(i => NewBooking($"B{i}", NewTicket($"T{i}", 1000m * i))).ToList();
        var service = new RefundBatchService(new FakeCandidates(trip, bookings), new FakeRequests(Result));

        var second = await service.ListCandidatesAsync(new(trip.Id, Page: 2, PageSize: 2));

        second.TotalCount.Should().Be(5);
        second.TotalPages.Should().Be(3);
        second.Items.Select(i => i.BookingCode).Should().Equal("B3", "B4");
    }
}
