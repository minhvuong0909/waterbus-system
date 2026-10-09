using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.UnitTests.AiBooking.Fakes;

/// <summary>
/// Fake adapter của IBookingLinkTool phục vụ kiểm thử đơn vị.
/// Trả kết quả do test cung cấp; không giả định domain, route hoặc query string của Web/Booking.
/// </summary>
public class FakeBookingLinkTool : IBookingLinkTool
{
    private readonly BookingLinkResult _configuredResult;

    public FakeBookingLinkTool(BookingLinkResult? configuredResult = null)
    {
        _configuredResult = configuredResult ?? new BookingLinkResult(
            BookingUrl: null,
            IsValid: false,
            Message: "Chưa có contract link đặt vé được owner Web/Booking xác nhận.");
    }

    public Task<BookingLinkResult> BuildBookingLinkAsync(
        BookingLinkCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (criteria.TripId == Guid.Empty)
        {
            return Task.FromResult(new BookingLinkResult(
                BookingUrl: null,
                IsValid: false,
                Message: "Mã chuyến tàu không hợp lệ (TripId rỗng)."));
        }

        return Task.FromResult(_configuredResult);
    }
}
