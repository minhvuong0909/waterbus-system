using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Captains;

public record AssignCaptainRequest(Guid? CaptainId, string Reason);
