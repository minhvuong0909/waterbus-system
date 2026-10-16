using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckout;
using WaterbusSystem.Application.Features.Bookings.Commands.CancelCheckout;
using WaterbusSystem.Application.Features.Bookings.Commands.ExpireCheckoutHolds;
using WaterbusSystem.Application.Features.Bookings.Commands.UpdateCheckoutSeats;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.Payments.Commands.CreateVnPayUrl;
using WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;
using WaterbusSystem.Application.Features.Refunds.Commands.RequestRefund;
using WaterbusSystem.Application.Features.Refunds.Commands.ConfirmManualRefund;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CommercialCheckoutTests
{
    internal sealed class Gateway : IVnPayService, IRefundGateway
    {
        public int RefundCalls; public Action? BeforeRefund;
        public RefundGatewayResult Outcome = RefundGatewayResult.Pending("refund-bank-reference");
        public VnPayQueryResult QueryResult = new();
        public string Name => "VNPAY"; public bool SupportsAutomaticRefund => true;
        public string CreatePaymentUrl(string reference, decimal amount, string info, string ip, DateTimeOffset? createdAt = null, DateTimeOffset? expiresAt = null) => reference;
        public bool ValidateSignature(IReadOnlyDictionary<string, string> q) => true;
        public bool IsValidTmnCode(string code) => true;
        public Task<VnPayQueryResult> QueryTransactionAsync(string r, string d, string ip, CancellationToken ct = default) => Task.FromResult(QueryResult);
        public Task<VnPayRefundResult> RefundAsync(VnPayRefundRequest r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RefundGatewayResult> RefundAsync(RefundGatewayRequest r, CancellationToken ct = default)
        { RefundCalls++; BeforeRefund?.Invoke(); return Task.FromResult(Outcome); }
    }
    internal static CreateBookingCommand Command(TestDbContextFactory.TripSeedData s) => new(s.Trip.Id, [s.Seat1.Id],
        s.StationA.Id, s.StationC.Id, "Buyer", "buyer@example.com", null);
    internal static ConfirmVnPayIpnCommand Ipn(FinancialTransaction attempt, string status = "00") => new(new Dictionary<string, string>
    {
        ["vnp_TmnCode"] = "test", ["vnp_TxnRef"] = attempt.MerchantReference!,
        ["vnp_TransactionNo"] = "12345", ["vnp_Amount"] = ((long)(attempt.Amount * 100)).ToString(),
        ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = status
    });
    internal static CreateBookingCommandHandler Builder(ApplicationDbContext c, ICommercialTransactionRunner? runner = null) =>
        CommercialHandlerFactory.Booking(c, transactions: runner);
    private static async Task<(BookingResponseDto Booking, FinancialTransaction Attempt, Gateway Gateway)> Pending(ApplicationDbContext c)
    {
        var seed = TestDbContextFactory.SeedTrip(c);
        var booking = await Builder(c).Handle(Command(seed), default);
        var g = new Gateway();
        var access = new CheckoutAccessService(c, new CheckoutOptions());
        await new CreateVnPayUrlCommandHandler(c, g, new DirectCommercialTransactionRunner(), new CheckoutOptions(), access)
            .Handle(new CreateVnPayUrlCommand(booking.OrderId, "127.0.0.1", booking.AccessToken), default);
        return (booking, c.FinancialTransactions.Single(), g);
    }

    [Fact]
    public async Task RoundTripCreatesTwoBookingsOneOrderAndOneDeadline()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var returnTrip = new Trip { Route = seed.Route, RouteId = seed.Route.Id, BoatId = seed.Boat.Id,
            DepartureTime = DateTimeOffset.UtcNow.AddDays(2), ArrivalTime = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30) };
        c.Trips.Add(returnTrip);
        foreach (var call in seed.Trip.TripStopCalls)
            c.TripStopCalls.Add(new TripStopCall { Trip = returnTrip, TripId = returnTrip.Id, RouteStop = call.RouteStop, VisitOrder = call.VisitOrder });
        await c.SaveChangesAsync();
        var result = await CommercialHandlerFactory.Checkout(c)
            .Handle(new CreateCheckoutCommand("Buyer", "buyer@example.com", null,
                [new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id),
                 new(returnTrip.Id, [seed.Seat2.Id], seed.StationA.Id, seed.StationC.Id)]), default);
        c.PurchaseOrders.Single().PurchaseMode.Should().Be(PurchaseMode.RoundTrip);
        result.Bookings.Should().HaveCount(2); result.QuotedTotal.Should().Be(30000);
        c.AccessGrants.Should().ContainSingle(); c.AccessGrants.Single().TokenHash.Should().NotBe(result.AccessToken);
        c.Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task PaymentAttemptRetryUsesSameReferenceAndDoesNotExtendDeadline()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        var order = c.PurchaseOrders.Single(); var deadline = order.ExpiresAt;
        var result = await new CreateVnPayUrlCommandHandler(c, state.Gateway, new DirectCommercialTransactionRunner(),
            new CheckoutOptions(), new CheckoutAccessService(c, new CheckoutOptions()))
            .Handle(new(state.Booking.OrderId, "127.0.0.1", state.Booking.AccessToken), default);
        result.MerchantReference.Should().Be(state.Attempt.MerchantReference);
        order.ExpiresAt.Should().Be(deadline); c.FinancialTransactions.Should().ContainSingle();
    }

    [Fact]
    public async Task ExpiredPaymentNeverResurrectsASeatSoldToSomeoneElse()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        c.PurchaseOrders.Single().ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await c.SaveChangesAsync();
        await new ExpireCheckoutHoldsCommandHandler(c, new DirectCommercialTransactionRunner(), new CheckoutOptions()).Handle(new(), default);
        var old = c.Bookings.Single(); var reservation = c.SeatReservations.Single();
        var newOrder = new PurchaseOrder { PurchaserName = "Other", PurchaserEmail = "other@example.com", QuotedTotal = 15000 };
        var other = new Booking { Order = newOrder, OrderId = newOrder.Id, BookingCode = "OTHER", Status = BookingStatus.PendingPayment, QuotedTotal = 15000 };
        other.SetJourney(old.BoardingCall!, old.DisembarkingCall!); c.Bookings.Add(other);
        c.SeatReservations.Add(new SeatReservation { Booking = other, BookingId = other.Id, TripId = old.TripId,
            SeatId = reservation.SeatId, BoardingStopOrder = 1, DisembarkingStopOrder = 3, PassengerName = "Other" });
        await c.SaveChangesAsync();
        (await CommercialHandlerFactory.Ipn(c, state.Gateway).Handle(Ipn(state.Attempt), default)).RspCode.Should().Be("00");
        reservation.Status.Should().Be(ReservationStatus.Cancelled); other.Status.Should().Be(BookingStatus.PendingPayment);
        c.Tickets.Should().BeEmpty(); state.Attempt.Status.Should().Be(FinancialTransactionStatus.Succeeded);
        state.Attempt.FailureReason.Should().StartWith("ReconciliationRequired:");
    }

    [Fact]
    public async Task PendingGatewayStatusDoesNotIssueTicketOrDestroyAttempt()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        (await CommercialHandlerFactory.Ipn(c, state.Gateway).Handle(Ipn(state.Attempt, "01"), default)).RspCode.Should().Be("99");
        c.Tickets.Should().BeEmpty(); state.Attempt.Status.Should().Be(FinancialTransactionStatus.Requested);
    }

    [Fact]
    public async Task CannotStartPaymentAfterTripBecomesUnavailable()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var booking = await Builder(c).Handle(Command(seed), default);
        seed.Trip.Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        var pay = new CreateVnPayUrlCommandHandler(c, new Gateway(), new DirectCommercialTransactionRunner(),
            new CheckoutOptions(), new CheckoutAccessService(c, new CheckoutOptions()));
        await Assert.ThrowsAsync<ConcurrencyException>(() => pay.Handle(new(booking.OrderId, "127.0.0.1", booking.AccessToken), default));
        c.FinancialTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingCheckoutDeadlineCannotIssueTicketsFromLegacyPendingOrder()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        c.PurchaseOrders.Single().ExpiresAt = null; await c.SaveChangesAsync();
        await CommercialHandlerFactory.Ipn(c, state.Gateway).Handle(Ipn(state.Attempt), default);
        c.Tickets.Should().BeEmpty(); state.Attempt.FailureReason.Should().StartWith("ReconciliationRequired:");
    }

    [Fact]
    public async Task ExpirationCleanupIsIdempotentAndDoesNotExpireAnActiveCheckout()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        var cleanup = new ExpireCheckoutHoldsCommandHandler(c, new DirectCommercialTransactionRunner(), new CheckoutOptions());
        (await cleanup.Handle(new(), default)).Should().Be(0);
        c.PurchaseOrders.Single().ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await c.SaveChangesAsync();
        (await cleanup.Handle(new(), default)).Should().Be(1);
        (await cleanup.Handle(new(), default)).Should().Be(0);
        c.SeatReservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task PaymentReconciliationRequiresExactAmountBeforeIssuingTickets()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        state.Gateway.QueryResult = new VnPayQueryResult { IsSuccess = true, MerchantReference = state.Attempt.MerchantReference,
            Amount = 1, TransactionType = "01", TransactionStatus = "00", TransactionNo = "query-success" };
        var handler = new WaterbusSystem.Application.Features.Payments.Commands.ReconcilePayment.ReconcilePaymentCommandHandler(c,
            new CheckoutAccessService(c, new CheckoutOptions()), state.Gateway, CommercialHandlerFactory.Confirmation(c, state.Gateway));
        var request = new WaterbusSystem.Application.Features.Payments.Commands.ReconcilePayment.ReconcilePaymentCommand(
            state.Booking.OrderId, state.Attempt.Id, state.Booking.AccessToken, "127.0.0.1");
        (await handler.Handle(request, default)).RspCode.Should().Be("99"); c.Tickets.Should().BeEmpty();
        state.Gateway.QueryResult = state.Gateway.QueryResult with { Amount = state.Attempt.Amount * 100 };
        (await handler.Handle(request, default)).RspCode.Should().Be("00"); c.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task RefundReconciliationMustMatchThisRefundRatherThanOriginalPayment()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        await CommercialHandlerFactory.Ipn(c, state.Gateway).Handle(Ipn(state.Attempt), default);
        c.Trips.Single().Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        var refund = await CommercialHandlerFactory.Refund(c, [state.Gateway]).Handle(new(state.Booking.BookingId, "cancelled"), default);
        state.Gateway.QueryResult = new VnPayQueryResult { IsSuccess = true, MerchantReference = state.Attempt.MerchantReference,
            Amount = refund.Amount * 100, TransactionType = "01", TransactionStatus = "00", TransactionNo = "12345" };
        var handler = new WaterbusSystem.Application.Features.Refunds.Commands.ReconcileRefund.ReconcileRefundCommandHandler(c, state.Gateway, new DirectCommercialTransactionRunner());
        (await handler.Handle(new(refund.RefundTransactionId, "127.0.0.1"), default)).Should().Be("Processing");
        state.Gateway.QueryResult = state.Gateway.QueryResult with { TransactionType = "02", TransactionNo = "refund-bank-reference" };
        (await handler.Handle(new(refund.RefundTransactionId, "127.0.0.1"), default)).Should().Be("Succeeded");
    }

    [Fact]
    public async Task CancelReleasesEveryHoldButRequiresTheOwnerToken()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var booking = await Builder(c).Handle(Command(seed), default);
        var cancel = new CancelCheckoutCommandHandler(c, new DirectCommercialTransactionRunner(), new CheckoutAccessService(c, new CheckoutOptions()));
        await Assert.ThrowsAsync<WaterbusSystem.Application.Common.Exceptions.ForbiddenAccessException>(() => cancel.Handle(new(booking.OrderId, "wrong"), default));
        await cancel.Handle(new(booking.OrderId, booking.AccessToken), default);
        c.SeatReservations.Should().OnlyContain(r => r.Status == ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task RefundIsPersistedBeforeGatewayAndReplayDoesNotSendAgain()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        await CommercialHandlerFactory.Ipn(c, state.Gateway).Handle(Ipn(state.Attempt), default);
        c.Trips.Single().Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        state.Gateway.BeforeRefund = () => c.FinancialTransactions.Should().Contain(t => t.TransactionType == FinancialTransactionType.Refund && t.Status == FinancialTransactionStatus.Processing);
        var handler = CommercialHandlerFactory.Refund(c, [state.Gateway]);
        var first = await handler.Handle(new(state.Booking.BookingId, "cancelled"), default);
        var retry = await handler.Handle(new(state.Booking.BookingId, "retry"), default);
        retry.RefundTransactionId.Should().Be(first.RefundTransactionId); state.Gateway.RefundCalls.Should().Be(1);
        first.Amount.Should().Be(state.Attempt.Amount);
    }

    [Fact]
    public async Task TicketRefundReferencesItsPaymentRatherThanALaterUnallocatedReceipt()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        await CommercialHandlerFactory.Ipn(c, state.Gateway).Handle(Ipn(state.Attempt), default);
        c.Trips.Single().Status = TripStatus.Cancelled;
        c.FinancialTransactions.Add(new FinancialTransaction { OrderId = state.Booking.OrderId,
            Amount = state.Attempt.Amount, Status = FinancialTransactionStatus.Succeeded,
            TransactionType = FinancialTransactionType.Payment, IdempotencyKey = "EXTRA_RECEIPT", GatewayName = "VNPAY",
            MerchantReference = "extra-receipt", CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1),
            FailureReason = "ReconciliationRequired: duplicate receipt without allocation" });
        await c.SaveChangesAsync();
        var result = await CommercialHandlerFactory.Refund(c, [state.Gateway]).Handle(new(state.Booking.BookingId, "cancelled"), default);
        c.FinancialTransactions.Single(p => p.Id == result.RefundTransactionId).OriginalPaymentId.Should().Be(state.Attempt.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredOrRevokedOwnerGrantCannotCancelCheckout(bool expired)
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var booking = await Builder(c).Handle(Command(seed), default); var grant = c.AccessGrants.Single();
        if (expired) grant.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); else grant.RevokedAt = DateTimeOffset.UtcNow;
        await c.SaveChangesAsync();
        await Assert.ThrowsAsync<WaterbusSystem.Application.Common.Exceptions.ForbiddenAccessException>(() =>
            new CancelCheckoutCommandHandler(c, new DirectCommercialTransactionRunner(), new CheckoutAccessService(c, new CheckoutOptions()))
                .Handle(new(booking.OrderId, booking.AccessToken), default));
        c.SeatReservations.Single().Status.Should().Be(ReservationStatus.Pending);
    }

    [Fact]
    public async Task ManualRefundCannotRaceAnUnresolvedAutomaticRefund()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        await CommercialHandlerFactory.Ipn(c, state.Gateway).Handle(Ipn(state.Attempt), default);
        c.Trips.Single().Status = TripStatus.Cancelled; await c.SaveChangesAsync();
        var refund = await CommercialHandlerFactory.Refund(c, [state.Gateway]).Handle(new(state.Booking.BookingId, "cancelled"), default);
        var admin = new Admin();
        await Assert.ThrowsAsync<ConcurrencyException>(() => CommercialHandlerFactory.ManualRefund(c, admin)
            .Handle(new(refund.RefundTransactionId, "bank transfer", "manual", refund.Amount), default));
    }
    private sealed class Admin : ICurrentUserService { public string? UserId => "00000000-0000-0000-0000-000000000001"; public string? UserRole => "Admin"; }

    [Fact]
    public async Task DraftHoldsCannotPayUntilPurchaserDetailsAreSubmitted()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var result = await new WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckoutHold.CreateCheckoutHoldCommandHandler(
            c, CommercialHandlerFactory.Creation(c), new DirectCommercialTransactionRunner(), CommercialHandlerFactory.Effects(c)).Handle(new([new(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id)]), default);
        result.Status.Should().Be("Draft"); c.Tickets.Should().BeEmpty();
        var access = new CheckoutAccessService(c, new CheckoutOptions());
        var pay = new CreateVnPayUrlCommandHandler(c, new Gateway(), new DirectCommercialTransactionRunner(), new CheckoutOptions(), access);
        await Assert.ThrowsAsync<ConcurrencyException>(() => pay.Handle(new(result.OrderId, "127.0.0.1", result.AccessToken), default));
        var deadline = result.ExpiresAt;
        await new WaterbusSystem.Application.Features.Bookings.Commands.SubmitCheckout.SubmitCheckoutCommandHandler(c, access, new DirectCommercialTransactionRunner())
            .Handle(new(result.OrderId, "Buyer", "buyer@example.com", null, AccessToken: result.AccessToken), default);
        c.PurchaseOrders.Single().ExpiresAt.Should().Be(deadline);
        c.SeatReservations.Single().PassengerName.Should().Be("Buyer");
        await pay.Handle(new(result.OrderId, "127.0.0.1", result.AccessToken), default);
        c.FinancialTransactions.Should().ContainSingle();
    }

    private sealed class Cache : ICheckoutSessionStore
    {
        private readonly Dictionary<string, string> _items = [];
        public Task<string?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult(_items.GetValueOrDefault(key));
        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) { _items[key] = value; return Task.CompletedTask; }
        public Task<bool> RemoveAsync(string key, CancellationToken ct = default) => Task.FromResult(_items.Remove(key));
    }
    [Fact]
    public async Task ChangingSeatsKeepsOriginalExpiryAndPaymentIssuesOnlyCurrentReservation()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var seed = TestDbContextFactory.SeedTrip(c);
        var result = await Builder(c).Handle(Command(seed), default); var order = c.PurchaseOrders.Single(); var deadline = order.ExpiresAt;
        var access = new CheckoutAccessService(c, new CheckoutOptions());
        var sessions = new CheckoutSessionService(new Cache(), new DateTimeService(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));
        await new UpdateCheckoutSeatsCommandHandler(c, new DirectCommercialTransactionRunner(), access, new SeatAvailabilityService(c), sessions)
            .Handle(new(result.OrderId, result.BookingId, [seed.Seat2.Id], [new("New passenger", null, null)], result.AccessToken), default);
        order.ExpiresAt.Should().Be(deadline);
        (await sessions.GetAsync("order:" + order.Id))!.ExpiresAt.Should().Be(deadline!.Value);
        var gateway = new Gateway();
        await new CreateVnPayUrlCommandHandler(c, gateway, new DirectCommercialTransactionRunner(), new CheckoutOptions(), access)
            .Handle(new(order.Id, "127.0.0.1", result.AccessToken), default);
        await CommercialHandlerFactory.Ipn(c, gateway).Handle(Ipn(c.FinancialTransactions.Single()), default);
        c.Tickets.Should().ContainSingle(); c.Tickets.Single().SeatReservation!.SeatId.Should().Be(seed.Seat2.Id);
        c.SeatReservations.Single(r => r.SeatId == seed.Seat1.Id).Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task LateReceiptCanBeRefundedWithoutIssuingTickets()
    {
        using var c = TestDbContextFactory.CreateDbContext(); var state = await Pending(c);
        c.PurchaseOrders.Single().ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await c.SaveChangesAsync();
        await CommercialHandlerFactory.Ipn(c, state.Gateway).Handle(Ipn(state.Attempt), default);
        state.Gateway.Outcome = RefundGatewayResult.Succeeded("returned");
        var first = await CommercialHandlerFactory.Refund(c, [state.Gateway]).Handle(new(state.Booking.BookingId, "late payment"), default);
        first.Amount.Should().Be(state.Attempt.Amount); first.Status.Should().Be("Succeeded"); c.Tickets.Should().BeEmpty();
    }

    [Fact]
    public void RsaQrVerifiesWithPublicKeyAndRejectsModifiedPayload()
    {
        using var rsa = RSA.Create(2048);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Qr:PrivateKeyPem"] = rsa.ExportPkcs8PrivateKeyPem(), ["Qr:KeyId"] = "test-key" }).Build();
        var signer = new RsaBookingQrSigner(configuration);
        var qr = signer.Sign("public-booking", 1, Guid.NewGuid(), DateTimeOffset.UtcNow).Payload.Split('.');
        byte[] Decode(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/') + new string('=', (4 - s.Length % 4) % 4));
        using var verifier = RSA.Create(); verifier.ImportFromPem(signer.GetPublicKey().PublicKeyPem);
        verifier.VerifyData(Encoding.ASCII.GetBytes(qr[0] + "." + qr[1]), Decode(qr[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Should().BeTrue();
        verifier.VerifyData(Encoding.ASCII.GetBytes(qr[0] + "." + qr[1] + "tampered"), Decode(qr[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Should().BeFalse();
    }
}
