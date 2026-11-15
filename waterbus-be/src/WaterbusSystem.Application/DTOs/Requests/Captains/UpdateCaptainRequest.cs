using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Captains;

public record UpdateCaptainRequest(string FullName, string Email, string Phone, string LicenseNumber, DateOnly LicenseExpiresOn, string Reason);
