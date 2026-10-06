using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Payments.Commands.CreateVnPayUrl;

public record CreateVnPayUrlResponseDto(string PaymentUrl, string OrderId, string MerchantReference, decimal Amount);

/// <summary>
/// BR-PAY-04: đơn vị thanh toán là PurchaseOrder, không phải từng Booking. Một Order round-trip
/// có 2 Booking nhưng chỉ một lần thanh toán, nên cổng thanh toán nhận tổng tiền của cả Order.
/// vnp_TxnRef gửi lên cổng là BookingCode của Booking đầu tiên thuộc Order - giá trị này
/// được IPN dùng để tra ngược lại đúng Order cần xác nhận.
/// </summary>
public record CreateVnPayUrlCommand(Guid OrderId, string IpAddress) : IRequest<CreateVnPayUrlResponseDto>;

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
        var order = await _context.PurchaseOrders
            .Include(o => o.Bookings)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.OrderId);

        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(CreateVnPayUrlCommand.OrderId), "Đơn hàng này không ở trạng thái chờ thanh toán.")
            });
        }

        // vnp_TxnRef là mã tham chiếu giao dịch, tối đa 100 ký tự alphanumeric.
        // Dùng BookingCode đầu tiên của Order làm mã tra ngược ở bước IPN.
        var merchantReference = order.Bookings
            .OrderBy(b => b.CreatedAt)
            .Select(b => b.BookingCode)
            .FirstOrDefault()
            ?? throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(CreateVnPayUrlCommand.OrderId), "Đơn hàng chưa có Booking nào để thanh toán.")
            });

        var orderInfo = $"Thanh toan ve buyt duong song {merchantReference}";
        var paymentUrl = _vnPayService.CreatePaymentUrl(merchantReference, order.QuotedTotal, orderInfo, request.IpAddress);

        return new CreateVnPayUrlResponseDto(paymentUrl, order.Id.ToString(), merchantReference, order.QuotedTotal);
    }
}