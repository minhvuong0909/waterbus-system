using FluentAssertions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Services.SightseeingAudio;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.AiBooking;

public sealed class AiModuleArchitectureTests
{
    [Fact]
    public void OwnedApplicationTypesHaveNoMediatorOrDatabaseDependencies()
    {
        var types = typeof(AudioGuideService).Assembly.GetTypes().Where(t =>
            t.Namespace?.Contains(".SightseeingAudio") == true || t.Namespace?.Contains(".AiBooking") == true);
        foreach (var type in types)
        {
            type.GetInterfaces().Should().NotContain(i => i.Namespace == "MediatR");
            var dependencies = type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);
            dependencies.Should().NotContain(t => t == typeof(IApplicationDbContext) ||
                t.Namespace == "MediatR" || (t.Namespace != null && t.Namespace.StartsWith("Microsoft.EntityFrameworkCore")));
        }
    }

    [Fact]
    public void AiControllersDependOnServiceInterfaces()
    {
        foreach (var type in new[] { typeof(AiAssistantController), typeof(TextToSpeechController), typeof(AudioGuidesController) })
        {
            var parameters = type.GetConstructors().Single().GetParameters();
            parameters.Should().NotBeEmpty();
            parameters.Should().OnlyContain(p => p.ParameterType.IsInterface &&
                p.ParameterType.Namespace == typeof(IAudioGuideService).Namespace);
        }
    }
}
