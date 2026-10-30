using FluentAssertions;
using WaterbusSystem.Application.Features.Boats.Commands.UpdateBoat;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.Boats;

/// <summary>
/// M10: lỗi ID không khớp phải throw để GlobalExceptionHandlingMiddleware trả ProblemDetails thống
/// nhất, không phải BadRequest(string) tự ý trả một JSON shape khác.
/// </summary>
public class BoatsControllerTests
{
    [Fact]
    public async Task UpdateBoat_WhenRouteIdDoesNotMatchBodyId_ShouldThrowArgumentException()
    {
        var controller = new BoatsController();
        var routeId = Guid.NewGuid();
        var command = new UpdateBoatCommand(Guid.NewGuid(), "Test", "REG", 10, "Mfg", 2024, true, null);

        var act = () => controller.UpdateBoat(routeId, command);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
