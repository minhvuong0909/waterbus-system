namespace WaterbusSystem.Infrastructure.Services.AiBooking;

/// <summary>
/// Cờ xác nhận contract từ owner Trip Search/Booking Web trước khi AI hiển thị dữ liệu thật.
/// </summary>
public sealed class AiBookingIntegrationOptions
{
    public const string SectionName = "AiBookingAssistant:Integration";

    public bool IsTripSearchContractVerified { get; set; }
    public bool IsBookingLinkContractVerified { get; set; }

    /// <summary>URL HTTPS đầy đủ do owner Web cung cấp, chứa đúng một {tripId}.</summary>
    public string? BookingUrlTemplate { get; set; }
}
