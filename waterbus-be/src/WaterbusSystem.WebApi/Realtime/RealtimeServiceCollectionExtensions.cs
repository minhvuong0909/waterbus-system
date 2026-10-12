using WaterbusSystem.Application.Common.Interfaces;
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

        return services;
    }

    public static IEndpointRouteBuilder MapRealtimeHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<BoatTrackingHub>(RealtimeContract.Paths.BoatTracking);
        endpoints.MapHub<SeatSyncHub>(RealtimeContract.Paths.SeatSync);
        return endpoints;
    }
}
