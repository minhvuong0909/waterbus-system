using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Responses.Captains;

public record CaptainSessionState(bool IsActive, bool IsCaptain, CaptainStatus? Status, Guid Version);
