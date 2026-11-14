using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.WebApi.Hubs;

namespace WaterbusSystem.WebApi.Realtime;

/// <summary>
/// Đăng ký SignalR + các publisher realtime và map hub.
/// </summary>
public static class RealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        services.AddSignalR();

        // IHubContext là singleton nên publisher cũng singleton được
        services.AddSingleton<IBoatTrackingPublisher, SignalRBoatTrackingPublisher>();
        services.AddSingleton<ISeatEventPublisher, SignalRSeatEventPublisher>();
        services.AddSingleton<IOperationsAlertPublisher, SignalROperationsAlertPublisher>();

        // Onboard Player (audio Sightseeing): registry in-process + dispatcher thật thay cho LogOnly mặc định của Infrastructure.
        services.AddSingleton<OnboardPlayerRegistry>();
        services.AddSingleton<IOnboardPlayerRegistry>(sp => sp.GetRequiredService<OnboardPlayerRegistry>());
        services.AddScoped<IOnboardAudioTriggerDispatcher, SignalROnboardAudioDispatcher>();

        return services;
    }

    public static IEndpointRouteBuilder MapRealtimeHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<BoatTrackingHub>(RealtimeContract.Paths.BoatTracking);
        endpoints.MapHub<SeatSyncHub>(RealtimeContract.Paths.SeatSync);
        endpoints.MapHub<OnboardAudioHub>(RealtimeContract.Paths.OnboardAudio);
        return endpoints;
    }
}
