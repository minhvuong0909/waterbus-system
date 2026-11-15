namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>Một điểm dừng của chuyến (theo <c>TripStopCall.VisitOrder</c>) kèm clip audio của 3 sự kiện bến.</summary>
public sealed record TripStopInfo(
    Guid TripStopCallId, Guid StationId, string StationName, int VisitOrder, decimal Latitude, decimal Longitude,
    IReadOnlyList<AudioClipRef> ApproachingClips, IReadOnlyList<AudioClipRef> ArrivedClips, IReadOnlyList<AudioClipRef> DepartedClips);

/// <summary>Ngữ cảnh chuyến + các điểm dừng đã sắp theo VisitOrder để quyết định thông báo bến.</summary>
public sealed record TripStationContext(TripSightseeingContext Trip, IReadOnlyList<TripStopInfo> Stops);

public sealed record StationAnnouncementResult(bool Triggered, string Message)
{
    public static StationAnnouncementResult Skipped(string message) => new(false, message);
}
