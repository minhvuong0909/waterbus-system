using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý đặt vé và khóa giữ chỗ tạm thời (Redis RedLock + Concurrency)
/// </summary>
public class BookingsController : BaseApiController
{
    /// <summary>
    /// Đặt vé và khóa ghế trong 10 phút. Ngăn chặn 100% tình trạng Overselling/Double-booking.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponseDto>> CreateBooking([FromBody] CreateBookingCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(CreateBooking), new { id = result.BookingId }, result);
    }
}
