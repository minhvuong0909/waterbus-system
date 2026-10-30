using FluentAssertions;
using WaterbusSystem.Application.Features.Boats.Queries.GetBoats;
using Xunit;

namespace WaterbusSystem.UnitTests.Boats;

/// <summary>M14: PageNumber/PageSize âm, bằng 0 hoặc quá lớn phải bị chặn trước khi vào Skip/Take.</summary>
public class GetBoatsQueryValidatorTests
{
    private static readonly GetBoatsQueryValidator Validator = new();

    [Fact]
    public void DefaultQuery_ShouldPass() =>
        Validator.Validate(new GetBoatsQuery()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePageNumber_ShouldFail(int pageNumber) =>
        Validator.Validate(new GetBoatsQuery(PageNumber: pageNumber)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void OutOfRangePageSize_ShouldFail(int pageSize) =>
        Validator.Validate(new GetBoatsQuery(PageSize: pageSize)).IsValid.Should().BeFalse();
}
