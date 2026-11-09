using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.OnboardAudio;
using WaterbusSystem.Application.DTOs.Responses.OnboardAudio;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Hợp đồng Backend → Onboard Player. Player đăng nhập bằng tài khoản Captain của chính Boat (BR không định nghĩa danh tính thiết bị);
/// mọi thao tác bị giới hạn ở Boat của Captain đó. Passenger/Guest/TripAccess không có đường nào vào đây (BR-ID-06, BR-EXP-02).
/// </summary>
public interface IOnboardAudioService
{
    Task<Guid> ResolveBoatAsync(Guid captainId, CancellationToken ct = default);
    Task<OnboardAudioStateResponse> GetStateAsync(Guid captainId, CancellationToken ct = default);
    /// <summary>Player xác nhận đã nhận lệnh. Trả false nếu không có dispatch nào đang chờ.</summary>
    Task<bool> AcceptAsync(Guid captainId, Guid requestId, CancellationToken ct = default);
    Task<PlaybackStateAck> ReportStateAsync(Guid captainId, Guid requestId, ReportPlaybackStateRequest request, CancellationToken ct = default);
    /// <summary>MP3 của lệnh phát; chỉ khi lệnh thuộc Boat của Captain và chuyến vẫn đang được phép phát.</summary>
    Task<StoredAudio> GetAudioAsync(Guid captainId, Guid requestId, CancellationToken ct = default);
}
