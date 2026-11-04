using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.Validators.Stations;
using Xunit;

namespace WaterbusSystem.UnitTests.Stations;

/// <summary>M14: PageNumber/PageSize âm, bằng 0 hoặc quá lớn phải bị chặn trước khi vào Skip/Take.</summary>
public class GetStationsPagedRequestValidatorTests
{
    private static readonly GetStationsPagedRequestValidator Validator = new();

    [Fact]
    public void DefaultRequest_ShouldPass() =>
        Validator.Validate(new GetStationsPagedRequest()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePageNumber_ShouldFail(int pageNumber) =>
        Validator.Validate(new GetStationsPagedRequest(PageNumber: pageNumber)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void OutOfRangePageSize_ShouldFail(int pageSize) =>
        Validator.Validate(new GetStationsPagedRequest(PageSize: pageSize)).IsValid.Should().BeFalse();
}
