using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.Infrastructure.Services.SightseeingAudio;

/// <summary>
/// Cài đặt tạm cho IOnboardAudioTriggerDispatcher: chỉ ghi log, chưa có loa tàu / IoT player thật.
/// Dùng cho development và demo — cho phép toàn bộ pipeline geofence → trigger hoạt động và test được
/// mà không cần phần cứng tàu. Khi có Onboard Player thật, thay bằng implementation gửi qua SignalR/MQTT.
/// </summary>
public sealed class LogOnlyAudioTriggerDispatcher : IOnboardAudioTriggerDispatcher
{
    private readonly ILogger<LogOnlyAudioTriggerDispatcher> _logger;

    public LogOnlyAudioTriggerDispatcher(ILogger<LogOnlyAudioTriggerDispatcher> logger)
    {
        _logger = logger;
    }

    public Task<AudioTriggerDispatchResult> DispatchTriggerRequestAsync(
        AudioTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "🔊 [LogOnly] Audio trigger dispatched — Trip={TripId}, POI={PoiName} ({PoiId}), " +
            "Boat={BoatId}, AudioAsset={AudioAssetUri}, Distance={Distance:F1}m, Radius={Radius}m",
            request.TripId, request.PoiName, request.PoiId,
            request.BoatId, request.AudioAssetUri,
            request.DistanceMeters, request.TriggerRadiusMeters);

        // Trả SentWithoutPlayerAck vì không có player thật xác nhận
        var result = AudioTriggerDispatchResult.SentWithoutPlayerAck(request.RequestId);
        return Task.FromResult(result);
    }
}
