using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.Validators.Boats;
using Xunit;

namespace WaterbusSystem.UnitTests.Boats;

/// <summary>M14: PageNumber/PageSize âm, bằng 0 hoặc quá lớn phải bị chặn trước khi vào Skip/Take.</summary>
public class GetBoatsRequestValidatorTests
{
    private static readonly GetBoatsRequestValidator Validator = new();

    [Fact]
    public void DefaultRequest_ShouldPass() =>
        Validator.Validate(new GetBoatsRequest()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePageNumber_ShouldFail(int pageNumber) =>
        Validator.Validate(new GetBoatsRequest(PageNumber: pageNumber)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void OutOfRangePageSize_ShouldFail(int pageSize) =>
        Validator.Validate(new GetBoatsRequest(PageSize: pageSize)).IsValid.Should().BeFalse();
}
