using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Captains;

public record CreateCaptainRequest(string FullName, string Email, string Phone, string LicenseNumber, DateOnly LicenseExpiresOn, string Password);
