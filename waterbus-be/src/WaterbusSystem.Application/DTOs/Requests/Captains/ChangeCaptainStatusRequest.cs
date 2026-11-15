using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Captains;

public record ChangeCaptainStatusRequest(CaptainStatus Status, string Reason);
