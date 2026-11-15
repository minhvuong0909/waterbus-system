using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>Bến của chuyến đang chạy, dùng để ghép thông báo và tìm địa danh tương ứng.</summary>
public sealed record StopAnnouncementStation(
    Guid StationId, string Name, int VisitOrder, decimal Latitude, decimal Longitude);

/// <summary>Ảnh chụp chuyến cần thiết để phát thông báo bến (chỉ đọc).</summary>
public sealed record StopAnnouncementSnapshot(
    Guid TripId,
    Guid BoatId,
    Guid RouteId,
    string TripType,
    IReadOnlyList<StopAnnouncementStation> Stations);

/// <summary>Địa danh trên tuyến kèm bài thuyết minh tiếng Việt đã publish (nếu có).</summary>
public sealed record RouteNarrationCandidate(
    Guid RoutePoiId,
    Guid PoiId,
    string PoiName,
    string? Description,
    decimal? Latitude,
    decimal? Longitude,
    int DisplayOrder,
    Guid? AudioGuideId,
    string? AudioAssetUri,
    int? DurationSeconds);
