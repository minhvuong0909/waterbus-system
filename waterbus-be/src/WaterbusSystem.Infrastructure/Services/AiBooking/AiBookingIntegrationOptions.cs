namespace WaterbusSystem.Infrastructure.Services.AiBooking;

/// <summary>
/// Cờ xác nhận contract từ owner Trip Search/Booking Web trước khi AI hiển thị dữ liệu thật.
/// </summary>
public sealed class AiBookingIntegrationOptions
{
    public const string SectionName = "AiBookingAssistant:Integration";

    public bool IsTripSearchContractVerified { get; set; }
    public bool IsBookingLinkContractVerified { get; set; }

    /// <summary>Chỉ dùng ở Development để xem card từ Trip Search khi giá/bến chưa được owner xác nhận.</summary>
    public bool EnableUnverifiedTripCardsForDevelopment { get; set; }

    /// <summary>URL HTTPS đầy đủ do owner Web cung cấp, chứa đúng một {tripId}.</summary>
    public string? BookingUrlTemplate { get; set; }
}
