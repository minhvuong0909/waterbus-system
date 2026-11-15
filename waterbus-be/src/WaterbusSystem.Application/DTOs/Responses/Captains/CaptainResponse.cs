using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Responses.Captains;

public record CaptainResponse(Guid Id, string FullName, string Email, string? Phone, string? LicenseNumber,
    DateOnly? LicenseExpiresOn, CaptainStatus Status, Guid? BoatId);
