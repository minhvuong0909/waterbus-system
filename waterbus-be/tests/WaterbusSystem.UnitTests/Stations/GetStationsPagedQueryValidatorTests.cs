using FluentAssertions;
using WaterbusSystem.Application.Features.Stations.Queries.GetStations;
using Xunit;

namespace WaterbusSystem.UnitTests.Stations;

/// <summary>M14: PageNumber/PageSize âm, bằng 0 hoặc quá lớn phải bị chặn trước khi vào Skip/Take.</summary>
public class GetStationsPagedQueryValidatorTests
{
    private static readonly GetStationsPagedQueryValidator Validator = new();

    [Fact]
    public void DefaultQuery_ShouldPass() =>
        Validator.Validate(new GetStationsPagedQuery()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePageNumber_ShouldFail(int pageNumber) =>
        Validator.Validate(new GetStationsPagedQuery(PageNumber: pageNumber)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void OutOfRangePageSize_ShouldFail(int pageSize) =>
        Validator.Validate(new GetStationsPagedQuery(PageSize: pageSize)).IsValid.Should().BeFalse();
}
