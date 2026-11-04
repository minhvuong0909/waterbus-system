namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record CheckInResultResponse(string Outcome, DateTimeOffset? CheckedInAt, string? TicketCode, string? PassengerName);
