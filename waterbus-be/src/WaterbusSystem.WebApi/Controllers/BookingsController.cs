using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WaterbusSystem.Application.DTOs.Requests.Bookings;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.WebApi.Filters;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Quản lý đặt vé và khóa giữ chỗ tạm thời (Redis RedLock + Concurrency)
/// </summary>
public class BookingsController(IBookingService service) : BaseApiController
{
    /// <summary>
    /// Đặt vé và khóa ghế trong 10 phút. Ngăn chặn 100% tình trạng Overselling/Double-booking.
    /// </summary>
    [HttpPost]
    [ServiceFilter(typeof(ValidateRequestFilter))]
    [EnableRateLimiting("BookingPolicy")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BookingResponse>> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var result = await service.CreateAsync(request, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(CheckoutsController.Get), "Checkouts", new { orderId = result.OrderId }, result);
    }
}
