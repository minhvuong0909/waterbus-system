using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Captains;

public record GetCaptainsRequest(int Page = 1, int PageSize = 20, string? Search = null, CaptainStatus? Status = null);
