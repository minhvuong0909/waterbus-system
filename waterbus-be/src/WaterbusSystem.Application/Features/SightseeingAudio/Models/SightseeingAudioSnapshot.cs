namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>Dữ liệu đọc từ repository; service áp dụng điều kiện nghiệp vụ trước khi trigger.</summary>
public record SightseeingAudioSnapshot(TripSightseeingContext Trip, IReadOnlyList<PoiGeofenceCandidate> Candidates);
