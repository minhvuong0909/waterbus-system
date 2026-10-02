using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Payments.Commands.CreateVnPayUrl;

public record CreateVnPayUrlResponseDto(string PaymentUrl, string BookingCode);

/// <summary>
/// Command sinh URL chuyển hướng khách hàng sang cổng thanh toán VNPAY
/// </summary>
public record CreateVnPayUrlCommand(Guid BookingId, string IpAddress) : IRequest<CreateVnPayUrlResponseDto>;

public class CreateVnPayUrlCommandHandler : IRequestHandler<CreateVnPayUrlCommand, CreateVnPayUrlResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IVnPayService _vnPayService;

    public CreateVnPayUrlCommandHandler(IApplicationDbContext context, IVnPayService vnPayService)
    {
        _context = context;
        _vnPayService = vnPayService;
    }

    public async Task<CreateVnPayUrlResponseDto> Handle(CreateVnPayUrlCommand request, CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId && !b.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), request.BookingId);

        var orderInfo = $"Thanh toan ve buyt duong song {booking.BookingCode}";
        var paymentUrl = _vnPayService.CreatePaymentUrl(booking.BookingCode, booking.TotalAmount, orderInfo, request.IpAddress);

        return new CreateVnPayUrlResponseDto(paymentUrl, booking.BookingCode);
    }
}
