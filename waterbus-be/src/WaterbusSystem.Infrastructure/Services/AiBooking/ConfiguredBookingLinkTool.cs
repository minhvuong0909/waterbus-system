using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Infrastructure.Services.AiBooking;

/// <summary>
/// Chỉ sinh link theo template HTTPS đã được owner Web/Booking xác nhận.
/// Không tự phỏng đoán đường dẫn và không tạo Booking/Payment.
/// </summary>
public sealed class ConfiguredBookingLinkTool : IBookingLinkTool
{
    private readonly IOptions<AiBookingIntegrationOptions> _options;

    public ConfiguredBookingLinkTool(IOptions<AiBookingIntegrationOptions> options) => _options = options;

    public Task<BookingLinkResult> BuildBookingLinkAsync(
        BookingLinkCriteria criteria, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(criteria);
        var options = _options.Value;
        var template = options.BookingUrlTemplate;

        if (!options.IsBookingLinkContractVerified || string.IsNullOrWhiteSpace(template))
            return Task.FromResult(new BookingLinkResult(null, false, "Hợp đồng booking link chưa được xác nhận."));

        if (criteria.TripId == Guid.Empty || criteria.PassengerCount is < 1 or > 50
            || (criteria.BoardingStationId.HasValue ^ criteria.DisembarkingStationId.HasValue))
            return Task.FromResult(new BookingLinkResult(null, false, "Tiêu chí booking link không hợp lệ."));

        const string placeholder = "{tripId}";
        if (template.Split(placeholder, StringSplitOptions.None).Length != 2
            || !template.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new BookingLinkResult(null, false, "Mẫu booking link chưa hợp lệ."));

        var hostPart = template["https://".Length..].Split('/', '?', '#')[0];
        if (hostPart.Contains('{') || hostPart.Contains('}'))
            return Task.FromResult(new BookingLinkResult(null, false, "Mẫu booking link chưa hợp lệ."));

        if (criteria.BoardingStationId.HasValue
            && (!template.Contains("{boardingStationId}", StringComparison.Ordinal)
                || !template.Contains("{disembarkingStationId}", StringComparison.Ordinal)))
            return Task.FromResult(new BookingLinkResult(null, false, "Mẫu link chưa chuyển đủ thông tin bến."));

        if (criteria.PassengerCount is > 1
            && !template.Contains("{passengerCount}", StringComparison.Ordinal))
            return Task.FromResult(new BookingLinkResult(null, false, "Mẫu link chưa chuyển số hành khách."));

        if ((template.Contains("{boardingStationId}", StringComparison.Ordinal)
                || template.Contains("{disembarkingStationId}", StringComparison.Ordinal))
            && !criteria.BoardingStationId.HasValue)
            return Task.FromResult(new BookingLinkResult(null, false, "Thiếu thông tin bến để tạo link."));

        var url = template
            .Replace(placeholder, criteria.TripId.ToString("D"), StringComparison.Ordinal)
            .Replace("{boardingStationId}", criteria.BoardingStationId?.ToString("D") ?? "", StringComparison.Ordinal)
            .Replace("{disembarkingStationId}", criteria.DisembarkingStationId?.ToString("D") ?? "", StringComparison.Ordinal)
            .Replace("{passengerCount}", (criteria.PassengerCount ?? 1).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        if (url.Contains('{') || url.Contains('}'))
            return Task.FromResult(new BookingLinkResult(null, false, "Mẫu booking link chưa hợp lệ."));
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
            return Task.FromResult(new BookingLinkResult(null, false, "Mẫu booking link chưa hợp lệ."));

        return Task.FromResult(new BookingLinkResult(uri.AbsoluteUri, true));
    }
}
