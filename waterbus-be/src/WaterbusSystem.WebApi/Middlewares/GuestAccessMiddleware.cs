using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.WebApi.Middlewares;

/// <summary>
/// Middleware xác thực Guest Access:
/// Kiểm tra header 'X-Guest-Token', băm SHA256 và tra cứu trong bảng Bookings (ManageOrderTokenHash).
/// Nếu hợp lệ, đính kèm OrderId / BookingId vào HttpContext.Items["GuestOrderId"] để Controller sử dụng.
/// </summary>
public class GuestAccessMiddleware
{
    public const string GuestTokenHeader = "X-Guest-Token";
    public const string GuestOrderIdItemKey = "GuestOrderId";
    public const string GuestBookingIdItemKey = "GuestBookingId";

    private readonly RequestDelegate _next;
    private readonly ILogger<GuestAccessMiddleware> _logger;

    public GuestAccessMiddleware(RequestDelegate next, ILogger<GuestAccessMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApplicationDbContext dbContext)
    {
        if (context.Request.Headers.TryGetValue(GuestTokenHeader, out var tokenValues))
        {
            var rawToken = tokenValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawToken))
            {
                var tokenHash = ComputeSha256Hash(rawToken);

                // Tra cứu đơn đặt theo ManageOrderTokenHash
                var booking = await dbContext.Bookings
                    .AsNoTracking()
                    .Where(b => !b.IsDeleted && b.ManageOrderTokenHash == tokenHash)
                    .Select(b => new
                    {
                        b.Id,
                        b.ManageOrderTokenExpiresAt,
                        b.ManageOrderTokenRevoked
                    })
                    .FirstOrDefaultAsync();

                if (booking != null
                    && booking.ManageOrderTokenExpiresAt.HasValue
                    && booking.ManageOrderTokenExpiresAt.Value > DateTimeOffset.UtcNow
                    && !booking.ManageOrderTokenRevoked)
                {
                    context.Items[GuestOrderIdItemKey] = booking.Id;
                    context.Items[GuestBookingIdItemKey] = booking.Id;
                    _logger.LogInformation("Guest Access token validated successfully for Booking {BookingId}", booking.Id);
                }
                else
                {
                    _logger.LogWarning("Invalid X-Guest-Token provided for path: {Path}", context.Request.Path);
                }
            }
        }

        await _next(context);
    }

    public static string ComputeSha256Hash(string rawData)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
