using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Validators.Bookings;
using FluentAssertions;
using MediatR;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Payments.Services;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Application.Features.Bookings.Services;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class CommercialArchitectureTests
{
    [Fact]
    public void CommercialServicesHaveNoMediatorOrDatabaseDependencies()
    {
        var assembly = typeof(BookingService).Assembly;
        var ownedTypes = assembly.GetTypes().Where(t => t.Namespace?.StartsWith("WaterbusSystem.Application.Features.") == true &&
            (t.Namespace.Contains(".Bookings.") || t.Namespace.Contains(".Payments.") || t.Namespace.Contains(".Refunds."))).ToList();
        ownedTypes.Should().NotBeEmpty();
        foreach (var type in ownedTypes)
        {
            type.GetInterfaces().Should().NotContain(i => i.Namespace == "MediatR");
            var dependencies = type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
                .Concat(type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Select(f => f.FieldType));
            dependencies.Should().NotContain(d => d == typeof(IApplicationDbContext) ||
                d.Namespace == "MediatR" || (d.Namespace != null && d.Namespace.StartsWith("Microsoft.EntityFrameworkCore")));
        }
    }

    [Fact]
    public void ProductionMoneyAndSeatWritersRequireATransactionRunner()
    {
        var types = new[] { typeof(BookingService), typeof(PaymentConfirmationService),
            typeof(RefundRequestService), typeof(ManualRefundService), typeof(RefundReconciliationService),
            typeof(CheckoutService), typeof(CheckoutSeatChangeService), typeof(CheckoutExpirationService), typeof(PaymentService) };
        foreach (var type in types)
        {
            var runner = type.GetConstructors().Single().GetParameters().Single(p => p.ParameterType == typeof(ICommercialTransactionRunner));
            runner.IsOptional.Should().BeFalse();
        }
        typeof(BookingService).Assembly.GetTypes().Should().NotContain(t => t.Name == "DirectCommercialTransactionRunner");
    }
}
