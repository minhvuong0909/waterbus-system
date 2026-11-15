using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Payments;
using WaterbusSystem.Application.Validators.Payments;
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
}
