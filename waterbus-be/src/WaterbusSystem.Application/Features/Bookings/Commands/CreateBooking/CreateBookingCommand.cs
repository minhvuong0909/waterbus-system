using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Application.Features.Bookings.Services;

namespace WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;

/// <summary>
/// Một hành khách sắp ngồi trên một ghế. BR-COM-06: tên bắt buộc, email/điện thoại tùy chọn.
/// BR-COM-05: Purchaser có thể mua vé cho người khác, không nhất thiết là người lên tàu.
/// </summary>
public record PassengerInput(string Name, string? Email, string? Phone);

/// <summary>
/// DTO trả về sau khi tạo Order + Booking và giữ chỗ thành công.
/// BookingId/TotalAmount/Status giữ nguyên tên cũ để các client hiện có không phải đổi.
/// </summary>
public record BookingResponseDto(
    Guid BookingId,
    string BookingCode,
    decimal TotalAmount,
    string Status,
    int HoldDurationSeconds,
    Guid OrderId = default,
    string OrderStatus = "",
    Guid BoardingCallId = default,
    Guid DisembarkingCallId = default,
    int BoardingStopOrder = 0,
    int DisembarkingStopOrder = 0,
    string? CheckoutSessionId = null,
    string? AccessToken = null);

/// <summary>
/// BR-HOLD-01/02: CheckoutSession được định danh theo Order chứ không theo UserId, nhờ vậy
/// Guest không cần đăng nhập vẫn dùng được, và cùng một phiên phục vụ được cho cả hai Booking
/// của một Order round-trip.
/// </summary>
public static class CheckoutSessionKey
{
    public static string ForOrder(Guid orderId) => $"order:{orderId}";
}

/// <summary>
/// BR-COM-08: Checkout Review tạo Order/Booking/SeatReservation ở trạng thái nháp,
/// CHƯA phát hành Ticket. Ticket chỉ được phát hành sau khi backend xác minh thanh toán thành công.
/// BR-ROUTE-02/06/07: Booking chỉ chọn cặp boarding - disembarking là hai TripStopCall thực sự
/// được phục vụ trên đúng Trip, đúng chiều di chuyển. Không suy từ Station.OrderIndex.
/// BR-SEAT-04/05: Ghế được giữ theo chặng [BoardingStopOrder, DisembarkingStopOrder) tính bằng
/// VisitOrder của TripStopCall, nên cùng một ghế vẫn bán lại được cho chặng không chồng lấn.
/// BR-FARE-01/02: Giá chỉ theo TripType x SeatClass, phẳng - không theo đoạn đi hay bến lên/xuống.
/// </summary>
public record CreateBookingCommand(
    Guid TripId,
    List<Guid> SeatIds,
    Guid BoardingStationId,
    Guid DisembarkingStationId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    IReadOnlyList<PassengerInput>? Passengers = null,
    Guid? BoardingCallId = null,
    Guid? DisembarkingCallId = null) : IRequest<BookingResponseDto>;

/// <summary>
/// Validator kiểm tra tính toàn vẹn của dữ liệu đầu vào
/// </summary>
public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty().WithMessage("Mã chuyến tàu không được để trống.");

        RuleFor(x => x.BoardingStationId)
            .NotEmpty().WithMessage("Bạn phải chọn bến lên tàu.");

        RuleFor(x => x.DisembarkingStationId)
            .NotEmpty().WithMessage("Bạn phải chọn bến xuống tàu.");

        RuleFor(x => x.SeatIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Bạn phải chọn ít nhất một ghế.")
            .Must(seats => seats.Count <= 10).WithMessage("Mỗi lượt đặt tối đa 10 ghế.")
            .Must(seats => seats.Distinct().Count() == seats.Count)
                .WithMessage("Danh sách ghế không được chứa ghế trùng lặp.");

        // BR-COM-06: nếu khách khai báo danh sách hành khách thì phải khớp 1-1 với ghế đã chọn.
        RuleFor(x => x.Passengers)
            .Must((cmd, p) => p == null || p.Count == 0 || p.Count == (cmd.SeatIds?.Count ?? 0))
                .WithMessage("Danh sách hành khách phải khớp với số ghế đã chọn.");

        RuleForEach(x => x.Passengers).ChildRules(passenger =>
        {
            passenger.RuleFor(p => p.Name).NotEmpty().MaximumLength(150);
            passenger.RuleFor(p => p.Email).MaximumLength(150).EmailAddress().When(p => !string.IsNullOrEmpty(p.Email));
            passenger.RuleFor(p => p.Phone).MaximumLength(20);
        });

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Họ và tên người mua không được để trống.")
            .MaximumLength(150).WithMessage("Họ tên không được vượt quá 150 ký tự.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Email người mua không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(150);

        RuleFor(x => x.CustomerPhone)
            .Matches(@"^[0-9]{10,11}$").WithMessage("Số điện thoại phải từ 10 đến 11 chữ số.")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone));
    }
}

/// <summary>Use case one-way: transaction trước, đồng bộ cache/event sau commit.</summary>
public sealed class CreateBookingCommandHandler(BookingCreationService creation, ICommercialTransactionRunner transactions,
    CheckoutCommitEffects effects) : IRequestHandler<CreateBookingCommand, BookingResponseDto>
{
    public async Task<BookingResponseDto> Handle(CreateBookingCommand request, CancellationToken ct)
    {
        var result = await transactions.RunAsync(() => creation.CreateAsync(request, ct), ct);
        await effects.HeldAsync(result.OrderId, ct);
        return result;
    }
}
