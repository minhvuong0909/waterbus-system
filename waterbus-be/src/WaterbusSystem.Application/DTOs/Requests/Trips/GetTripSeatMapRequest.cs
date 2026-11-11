namespace WaterbusSystem.Application.DTOs.Requests.Trips;

public record GetTripSeatMapRequest(Guid BoardingCallId, Guid DisembarkingCallId);
