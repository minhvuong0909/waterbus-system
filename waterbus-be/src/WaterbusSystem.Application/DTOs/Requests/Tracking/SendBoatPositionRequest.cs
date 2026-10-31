namespace WaterbusSystem.Application.DTOs.Requests.Tracking;

/// <summary>Body Captain gửi lên khi báo vị trí qua REST (tàu và Captain lấy từ route và token).</summary>
public record SendBoatPositionRequest(decimal Latitude, decimal Longitude, decimal? SpeedKnots, DateTimeOffset ObservedAt);
