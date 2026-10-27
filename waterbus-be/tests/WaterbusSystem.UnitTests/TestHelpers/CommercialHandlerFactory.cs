using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateCheckout;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;
using WaterbusSystem.Application.Features.Payments.Services;
using WaterbusSystem.Application.Features.Refunds.Commands.RequestRefund;
using WaterbusSystem.Application.Features.Refunds.Commands.ConfirmManualRefund;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.UnitTests.TestHelpers;

/// <summary>Không giả transaction trong assembly sản phẩm; test InMemory chọn fake tường minh.</summary>
public sealed class DirectCommercialTransactionRunner : ICommercialTransactionRunner
{
    public Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
}

internal static class CommercialHandlerFactory
{

    internal static ICheckoutSeatChangeService SeatChanges(
        ApplicationDbContext context)
    {
        return new CheckoutSeatChangeService(
            new OrderRepository(context),
            new BookingReferenceRepository(context),
            new BookingRepository(context),
            new SeatAvailabilityService(context),
            new CheckoutAccessService(new AccessGrantRepository(context), new CheckoutOptions()),
            new DirectCommercialTransactionRunner(),
            new EfUnitOfWork(context),
            Effects(context),
            new UpdateCheckoutSeatsRequestValidator());
    }


    internal static BookingCreationService Creation(
    ApplicationDbContext context,
    IDistributedLockService? locks = null,
    ISeatAvailabilityService? availability = null)
    {
        return new BookingCreationService(
            new OrderRepository(context),
            new BookingRepository(context),
            new EfUnitOfWork(context),
            new BookingReferenceRepository(context),
            locks ?? new FakeDistributedLockService(),
            availability ?? new SeatAvailabilityService(context),
            new CheckoutOptions(),
            new CheckoutAccessService(new AccessGrantRepository(context),
                new CheckoutOptions()));
    }
    internal static CheckoutCommitEffects Effects(
        ApplicationDbContext context,
        ICheckoutSessionService? sessions = null,
        ISeatEventPublisher? events = null)
    {
        return new CheckoutCommitEffects(
            new OrderRepository(context),
            new BookingRepository(context),
            sessions ?? new CheckoutSessionService(
                new MemoryStore(),
                new Clock(),
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(15)),
            NullLogger<CheckoutCommitEffects>.Instance,
            events);
    }

    internal static CreateBookingCommandHandler Booking(ApplicationDbContext context, IDistributedLockService? locks = null,
        ISeatAvailabilityService? availability = null, ICheckoutSessionService? checkoutSessionService = null,
        ICommercialTransactionRunner? transactions = null) => new(Creation(context, locks, availability),
            transactions ?? new DirectCommercialTransactionRunner(), Effects(context, checkoutSessionService));

    internal static CreateCheckoutCommandHandler Checkout(ApplicationDbContext context, ICommercialTransactionRunner? transactions = null,
        ICheckoutSessionService? sessions = null) => new(context, Creation(context), transactions ?? new DirectCommercialTransactionRunner(), Effects(context, sessions));

    internal static PaymentConfirmationService Confirmation(ApplicationDbContext context, IVnPayService gateway,
        ICommercialTransactionRunner? transactions = null, ICheckoutSessionService? sessions = null) =>
        new(context, gateway, transactions ?? new DirectCommercialTransactionRunner(), sessions);

    internal static ConfirmVnPayIpnCommandHandler Ipn(ApplicationDbContext context, IVnPayService gateway,
        ICommercialTransactionRunner? transactions = null) => new(Confirmation(context, gateway, transactions));

    internal static RequestRefundCommandHandler Refund(ApplicationDbContext context, IEnumerable<IRefundGateway> gateways,
        ICommercialTransactionRunner? transactions = null) => new(context, gateways, transactions ?? new DirectCommercialTransactionRunner());

    internal static ConfirmManualRefundCommandHandler ManualRefund(ApplicationDbContext context, ICurrentUserService currentUser,
        ICommercialTransactionRunner? transactions = null) => new(context, currentUser, transactions ?? new DirectCommercialTransactionRunner());

    private sealed class Clock : IDateTimeService { public DateTimeOffset Now => DateTimeOffset.UtcNow; }
    internal sealed class MemoryStore : ICheckoutSessionStore
    {
        public readonly Dictionary<string, string> Items = [];
        public Task<string?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult(Items.GetValueOrDefault(key));
        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) { Items[key] = value; return Task.CompletedTask; }
        public Task<bool> RemoveAsync(string key, CancellationToken ct = default) => Task.FromResult(Items.Remove(key));
    }
}
