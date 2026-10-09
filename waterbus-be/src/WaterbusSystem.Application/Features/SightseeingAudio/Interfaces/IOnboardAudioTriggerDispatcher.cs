using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;

/// <summary>
/// Interface gửi yêu cầu / lệnh kích hoạt phát âm thanh tới Onboard Player trên tàu.
/// Tách riêng khỏi backend geofence logic để sau này nối với teammate phụ trách hub/IoT player.
/// LƯU Ý: Dịch vụ này chỉ gửi lệnh (dispatch); KHÔNG được tự ý xem là âm thanh đã phát ra loa (BR-EXP-03).
/// </summary>
public interface IOnboardAudioTriggerDispatcher
{
    /// <summary>
    /// Gửi yêu cầu phát âm thanh tới Onboard Player trên tàu.
    /// </summary>
    /// <param name="request">Thông tin yêu cầu kích hoạt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Kết quả gửi yêu cầu (đã dispatch thành công tới transport layer).</returns>
    Task<AudioTriggerDispatchResult> DispatchTriggerRequestAsync(
        AudioTriggerRequest request,
        CancellationToken cancellationToken = default);
}
