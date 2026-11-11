using WaterbusSystem.Application.Interfaces.Services;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WaterbusSystem.Application.Services.SightseeingAudio;

namespace WaterbusSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Đăng ký toàn bộ Validators của FluentValidation
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<IAudioAssetGenerationService, AudioAssetGenerationService>();
        services.AddScoped<IAudioSynthesisService, AudioSynthesisService>();
        services.AddScoped<IAudioAssetService, AudioAssetService>();
        services.AddScoped<IAudioGuideService, AudioGuideService>();

        return services;
    }
}
