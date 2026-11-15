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
    private readonly CreateVnPayUrlRequestValidator _sut = new();

    [Fact]
    public void ValidOrderId_IsAccepted() =>
        _sut.Validate(new CreateVnPayUrlRequest(Guid.NewGuid())).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyOrderId_IsRejectedWithAClearMessage()
    {
        var result = _sut.Validate(new CreateVnPayUrlRequest(Guid.Empty));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "OrderId" && e.ErrorMessage.Contains("Mã đơn hàng"));
    }

    private static bool HasValidateFilter(string action) =>
        typeof(PaymentsController).GetMethod(action)!.GetCustomAttributes<ServiceFilterAttribute>()
            .Any(a => a.ServiceType == typeof(ValidateRequestFilter));

    [Fact]
    public void OnlyCreateVnPayUrl_RunsTheRequestValidator()
    {
        HasValidateFilter(nameof(PaymentsController.CreateVnPayUrl)).Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(PaymentsController.VnPayIpn))]
    [InlineData(nameof(PaymentsController.VnPayReturn))]
    [InlineData(nameof(PaymentsController.Reconcile))]
    public void GatewayCallbacksAndReconcile_DoNotGoThroughTheRequestValidator(string action)
    {
        // IPN/return của VNPAY mang dữ liệu trong query và được xác thực bằng chữ ký HMAC; validator DTO không được chen vào.
        HasValidateFilter(action).Should().BeFalse();
    }
}
