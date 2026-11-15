using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Captains;

public record GetCaptainEventsRequest(int Page = 1, int PageSize = 20, Guid? CaptainId = null, Guid? TripId = null);
