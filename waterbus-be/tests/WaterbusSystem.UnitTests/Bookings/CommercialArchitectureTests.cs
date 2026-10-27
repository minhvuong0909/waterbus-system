using FluentAssertions;
using MediatR;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Features.Payments.Services;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.Refunds.Commands.ConfirmManualRefund;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CommercialArchitectureTests
{
    [Fact]
    public void CommercialHandlersDoNotUseOtherCommandHandlersAsDependencies()
    {
        var assembly = typeof(CreateBookingCommandHandler).Assembly;
        var handlers = assembly.GetTypes().Where(t => t.Namespace?.StartsWith("WaterbusSystem.Application.Features.") == true &&
            (t.Namespace.Contains(".Bookings.") || t.Namespace.Contains(".Payments.") || t.Namespace.Contains(".Refunds.")) &&
            t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));
        foreach (var handler in handlers)
            foreach (var dependency in handler.GetConstructors().SelectMany(c => c.GetParameters()))
                dependency.ParameterType.GetInterfaces().Should().NotContain(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>), $"{handler.Name} phải dùng service chung, không gọi handler khác");
    }

    [Fact]
    public void ProductionMoneyAndSeatWritersRequireATransactionRunner()
    {
        var types = new[] { typeof(CreateBookingCommandHandler), typeof(PaymentConfirmationService),
            typeof(RefundRequestService), typeof(ManualRefundService), typeof(RefundReconciliationService),
            typeof(BookingService), typeof(CheckoutService), typeof(CheckoutSeatChangeService), typeof(CheckoutExpirationService), typeof(PaymentService) };
        foreach (var type in types)
        {
            var runner = type.GetConstructors().Single().GetParameters().Single(p => p.ParameterType == typeof(ICommercialTransactionRunner));
            runner.IsOptional.Should().BeFalse();
        }
        typeof(CreateBookingCommandHandler).Assembly.GetTypes().Should().NotContain(t => t.Name == "DirectCommercialTransactionRunner");
    }
}
