using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.DTOs.Requests.Payments;
using WaterbusSystem.Application.Validators.Payments;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Filters;
using Xunit;

namespace WaterbusSystem.UnitTests.Payments;

public class CreateVnPayUrlRequestValidatorTests
{
    private readonly CreateVnPayUrlRequestValidator _validator = new();

    [Fact]
    public void ValidOrderId_ShouldPass() =>
        _validator.Validate(new CreateVnPayUrlRequest(Guid.NewGuid())).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyOrderId_ShouldFail() =>
        _validator.Validate(new CreateVnPayUrlRequest(Guid.Empty)).Errors
            .Should().Contain(e => e.PropertyName == "OrderId");

    private static bool HasValidateFilter(string action) =>
        typeof(PaymentsController).GetMethod(action)!.GetCustomAttributes<ServiceFilterAttribute>()
            .Any(a => a.ServiceType == typeof(ValidateRequestFilter));

    [Fact]
    public void OnlyCreateVnPayUrl_RunsTheRequestValidator() =>
        HasValidateFilter(nameof(PaymentsController.CreateVnPayUrl)).Should().BeTrue();

    [Theory]
    [InlineData(nameof(PaymentsController.VnPayIpn))]
    [InlineData(nameof(PaymentsController.VnPayReturn))]
    [InlineData(nameof(PaymentsController.Reconcile))]
    public void GatewayCallbacksAndReconcile_DoNotGoThroughTheRequestValidator(string action) =>
        HasValidateFilter(action).Should().BeFalse();
}
