using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>Đọc ngữ cảnh Trip/POI từ DB rồi đánh giá một vị trí đã được tracking chấp nhận.</summary>
public interface ITrackingAudioTriggerEvaluator
{
    Task<PoiAudioTriggerEvaluationResult?> EvaluateAsync(
        BoatPositionSnapshot position, CancellationToken cancellationToken = default);
}
