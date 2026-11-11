namespace WaterbusSystem.Application.DTOs.Requests.Tracking;

public record SendBoatPositionRequest(
    decimal Latitude, 
    decimal Longitude, 
    decimal? SpeedKnots, 
    DateTimeOffset ObservedAt);
