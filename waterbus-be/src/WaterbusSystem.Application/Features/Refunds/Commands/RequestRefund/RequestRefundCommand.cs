using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Refunds.Commands.RequestRefund;

/// <summary>Kết quả trả về cho Admin sau khi tạo yêu cầu hoàn tiền.</summary>
public sealed record RequestRefundResultDto(
    Guid RefundTransactionId,
    Guid OrderId,
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Status,
    string Gateway,
    bool RequiresManualAction,
    IReadOnlyList<RefundTicketAllocationDto> Tickets);

public sealed record RefundTicketAllocationDto(Guid TicketId, string TicketCode, decimal Amount, string Reason);

/// <summary>
/// BR-RF-01 → BR-RF-10: tạo yêu cầu hoàn tiền cho MỘT Booking bị ảnh hưởng bởi việc Trip bị
/// Cancelled hoặc Terminated.
///
/// BR-RF-08: nếu Order round-trip có 2 Booking nhưng chỉ một Booking bị ảnh hưởng thì chỉ hoàn
/// phần phân bổ cho Booking đủ điều kiện - Booking còn lại giữ nguyên kết quả.
/// BR-RF-09: trong Booking nhóm, xét từng Ticket. Ticket NoShow không hoàn; những Ticket khác
/// chưa được phục vụ hoặc bị gián đoạn thì hoàn 100% paid allocation của chính Ticket đó.
/// BR-PAY-06: tổng các lần hoàn trên cùng một khoản thanh toán không được vượt quá số tiền còn
/// có thể hoàn, và không được hoàn trùng một Ticket.
///
/// <paramref name="CreatedBy"/> là người khởi tạo hoàn tiền (VNPAY yêu cầu vnp_CreateBy để đối soát);
/// <paramref name="IpAddress"/> là IP máy chủ gọi API (vnp_IpAddr). Cả hai do controller chuyển vào.
/// </summary>
public record RequestRefundCommand(
    Guid BookingId,
    string Reason,
    string? IpAddress = null,
    string? CreatedBy = null) : IRequest<RequestRefundResultDto>;

public sealed class RequestRefundCommandValidator : AbstractValidator<RequestRefundCommand>
{
    public RequestRefundCommandValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}

public class RequestRefundCommandHandler : IRequestHandler<RequestRefundCommand, RequestRefundResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<IRefundGateway> _refundGateways;
    private readonly ICommercialTransactionRunner _transactions;

    public RequestRefundCommandHandler(IApplicationDbContext context, IEnumerable<IRefundGateway> refundGateways, ICommercialTransactionRunner transactions)
    {
        _context = context;
        _refundGateways = refundGateways;
        _transactions = transactions;
    }

    public async Task<RequestRefundResultDto> Handle(RequestRefundCommand request, CancellationToken cancellationToken)
    {
        var prepared = await _transactions.RunAsync(() => PrepareAsync(request, cancellationToken), cancellationToken);
        if (prepared.Request is not null)
        {
            RefundGatewayResult outcome;
            try { outcome = await prepared.Gateway.RefundAsync(prepared.Request, cancellationToken); }
            catch (Exception) { outcome = RefundGatewayResult.Pending(); }
            await _transactions.RunAsync(async () =>
            {
                ApplyGatewayOutcome(prepared.Refund, outcome, DateTimeOffset.UtcNow);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }
        var refund = prepared.Refund;
        return new RequestRefundResultDto(refund.Id, refund.OrderId, refund.RefundBookingId!.Value,
            refund.Amount, refund.Currency, refund.Status.ToString(), refund.GatewayName ?? "",
            refund.Status == FinancialTransactionStatus.Requested || refund.GatewayName == "Manual", prepared.Allocations);
    }

    private record PreparedRefund(FinancialTransaction Refund, IRefundGateway Gateway,
        RefundGatewayRequest? Request, IReadOnlyList<RefundTicketAllocationDto> Allocations);

    private async Task<PreparedRefund> PrepareAsync(RequestRefundCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var booking = await _context.Bookings
            .Include(b => b.Trip).ThenInclude(t => t!.TripStopCalls)
            .Include(b => b.Tickets)
            .Include(b => b.SeatReservations)
            .Include(b => b.Order)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), request.BookingId);

        if (booking.Order == null || booking.Trip == null)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundCommand.BookingId), "Booking chưa gắn đủ Order hoặc Trip nên không thể hoàn tiền.")
            });
        }

        var existing = await _context.FinancialTransactions.Include(r => r.RefundAllocations).ThenInclude(a => a.Ticket)
            .FirstOrDefaultAsync(r => r.RefundBookingId == booking.Id && r.TransactionType == FinancialTransactionType.Refund &&
                (r.Status == FinancialTransactionStatus.Requested || r.Status == FinancialTransactionStatus.Processing ||
                 r.Status == FinancialTransactionStatus.Succeeded), cancellationToken);
        if (existing is not null)
            return new PreparedRefund(existing, SelectGateway(), null, existing.RefundAllocations
                .Select(a => new RefundTicketAllocationDto(a.TicketId, a.Ticket!.TicketCode, a.Amount, "Existing refund")).ToList());

        var trip = booking.Trip;

        // BR-PAY-03: Refund phải tham chiếu một Payment thành công thuộc cùng Order.
        var payment = await _context.FinancialTransactions
            .Where(t => t.OrderId == booking.OrderId
                        && t.TransactionType == FinancialTransactionType.Payment
                        && t.Status == FinancialTransactionStatus.Succeeded
                        && (booking.Tickets.Count == 0 || t.FailureReason == null || !t.FailureReason.StartsWith("ReconciliationRequired:")))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment == null)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundCommand.BookingId),
                    "Chưa có giao dịch thanh toán thành công nào cho đơn hàng này nên không thể hoàn tiền.")
            });
        }

        var tickets = booking.Tickets.ToList();
        if (tickets.Count == 0 && payment.FailureReason?.StartsWith("ReconciliationRequired:") == true)
            return await PrepareLatePaymentRefundAsync(booking, payment, request, cancellationToken);
        if (tickets.Count == 0)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundCommand.BookingId), "Booking chưa được phát hành vé nên không có gì để hoàn.")
            });
        }

        // BR-RF-09: quyết định theo TỪNG Ticket, không phải cả Booking một lượt.
        var decisions = tickets
            .Select(t => (Ticket: t, Decision: RefundEligibilityEvaluator.Evaluate(t, booking, trip)))
            .ToList();

        var eligible = decisions.Where(d => d.Decision.IsEligible).ToList();
        if (eligible.Count == 0)
        {
            var reasons = string.Join(" | ", decisions.Select(d => $"{d.Ticket.TicketCode}: {d.Decision.Reason}"));
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundCommand.BookingId), $"Không có vé nào đủ điều kiện hoàn tiền. {reasons}")
            });
        }

        // BR-PAY-06: số tiền hoàn tính theo paid allocation, không vượt số đã thu còn có thể hoàn.
        var refundableAmount = 0m;

        var alreadyRefundedTicketIds = await _context.RefundTicketAllocations
            .AsNoTracking()
            .Where(a => a.RefundTransaction != null
                        && a.RefundTransaction.OrderId == booking.OrderId
                        && a.RefundTransaction.TransactionType == FinancialTransactionType.Refund
                        && (a.RefundTransaction.Status == FinancialTransactionStatus.Succeeded
                            || a.RefundTransaction.Status == FinancialTransactionStatus.Processing
                            || a.RefundTransaction.Status == FinancialTransactionStatus.Requested))
            .Select(a => a.TicketId)
            .ToListAsync(cancellationToken);

        var refundableTickets = eligible
            .Where(d => !alreadyRefundedTicketIds.Contains(d.Ticket.Id))
            .ToList();

        if (refundableTickets.Count == 0)
        {
            throw new ConcurrencyException("Các vé này đã có yêu cầu hoàn tiền trước đó. Không được hoàn trùng.");
        }

        refundableAmount = refundableTickets.Sum(d => d.Ticket.PaidFareSnapshot);

        // Số tiền còn có thể hoàn = tiền đã thu - tổng các khoản đã hoàn/đang xử lý.
        var settledRefundTotal = await _context.FinancialTransactions.AsNoTracking()
            .Where(r => r.OriginalPaymentId == payment.Id && r.TransactionType == FinancialTransactionType.Refund &&
                (r.Status == FinancialTransactionStatus.Requested || r.Status == FinancialTransactionStatus.Processing ||
                 r.Status == FinancialTransactionStatus.Succeeded))
            .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;

        var refundableBalance = payment.Amount - settledRefundTotal;
        if (refundableAmount > refundableBalance)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundCommand.BookingId),
                    $"Số tiền cần hoàn {refundableAmount:N0} vượt quá số tiền còn có thể hoàn {refundableBalance:N0}.")
            });
        }

        var gateway = SelectGateway();

        // BR-RF-07 / BR-PAY-03: Refund là một FinancialTransaction có type = Refund, tham chiếu
        // Payment gốc và Booking bị ảnh hưởng, có IdempotencyKey chống tạo trùng.
        var refund = new FinancialTransaction
        {
            OrderId = booking.OrderId,
            Order = booking.Order,
            RefundBookingId = booking.Id,
            RefundBooking = booking,
            OriginalPaymentId = payment.Id,
            OriginalPayment = payment,
            TransactionType = FinancialTransactionType.Refund,
            Amount = refundableAmount,
            Currency = payment.Currency,
            Status = FinancialTransactionStatus.Processing,
            GatewayName = gateway.Name,
            MerchantReference = payment.MerchantReference,
            IdempotencyKey = $"REFUND_{booking.Id:N}",
            ProcessedAt = now
        };

        var allocations = new List<RefundTicketAllocationDto>(refundableTickets.Count);

        // BR-STATE-02: chuyến chưa bắt đầu thì hủy, chuyến đã chạy mà kết thúc sớm thì terminated.
        var terminalTicketStatus = trip.Status == TripStatus.Terminated
            ? TicketStatus.Terminated
            : TicketStatus.Cancelled;

        foreach (var item in refundableTickets)
        {
            refund.RefundAllocations.Add(new RefundTicketAllocation
            {
                RefundTransaction = refund,
                RefundTransactionId = refund.Id,
                TicketId = item.Ticket.Id,
                Ticket = item.Ticket,
                // BR-RF-09: hoàn 100% paid allocation của Ticket đủ điều kiện, không prorate.
                Amount = item.Ticket.PaidFareSnapshot
            });

            allocations.Add(new RefundTicketAllocationDto(
                item.Ticket.Id, item.Ticket.TicketCode, item.Ticket.PaidFareSnapshot, item.Decision.Reason));

            // Vé không còn quyền sử dụng sau khi chuyến bị hủy/chấm dứng.
            item.Ticket.Status = terminalTicketStatus;
        }

        /// BR-PAY-03/04/06 + BR-RF-09: kiểm tra ở tầng Domain trước khi ghi xuống CSDL.
        refund.ValidateRefund(payment, booking, refundableBalance);

        // BR-RF-01/04: Booking của nhóm bị ảnh hưởng thì chuyển trạng thái; Booking còn lại của
        // Order round-trip giữ nguyên vì chỉ hoàn phần đủ điều kiện (BR-RF-08).
        booking.Status = trip.Status == TripStatus.Cancelled
            ? BookingStatus.Cancelled
            : BookingStatus.Terminated;

        var gatewayRequest = new RefundGatewayRequest
            {
                MerchantReference = payment.MerchantReference ?? payment.GatewayReference ?? booking.BookingCode,
                GatewayReference = payment.GatewayReference,
                RequestId = refund.Id.ToString("N"),
                TransactionDate = (payment.GatewayRequestCreatedAt ?? throw new InvalidOperationException("Original payment request time missing")).ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"),
                Amount = refundableAmount,
                CreatedBy = request.CreatedBy ?? "system",
                // vnp_IpAddr là trường bắt buộc của API VNPAY (độ dài 7-45), nên không được để rỗng.
                IpAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? "127.0.0.1" : request.IpAddress,
                OrderInfo = "Refund waterbus " + refund.Id.ToString("N"),
                IsFullRefund = refundableAmount == payment.Amount
            };
        _context.FinancialTransactions.Add(refund);
        await _context.SaveChangesAsync(cancellationToken);
        return new PreparedRefund(refund, gateway, gatewayRequest, allocations);
    }

    private async Task<PreparedRefund> PrepareLatePaymentRefundAsync(Booking booking, FinancialTransaction payment,
        RequestRefundCommand request, CancellationToken ct)
    {
        // Tiền nhận sau expiry không có Ticket allocation: hoàn toàn bộ receipt không có quyền đi tàu.
        if (await _context.Tickets.AnyAsync(t => t.Booking!.OrderId == booking.OrderId, ct))
            throw new ConcurrencyException("Khoản payment muộn có vé cần đối soát riêng.");
        var existing = await _context.FinancialTransactions.Include(r => r.RefundAllocations)
            .FirstOrDefaultAsync(r => r.OriginalPaymentId == payment.Id && r.TransactionType == FinancialTransactionType.Refund, ct);
        var gateway = SelectGateway();
        if (existing is not null) return new PreparedRefund(existing, gateway, null, []);
        var refund = new FinancialTransaction { Order = booking.Order, OrderId = booking.OrderId,
            RefundBooking = booking, RefundBookingId = booking.Id, OriginalPayment = payment, OriginalPaymentId = payment.Id,
            TransactionType = FinancialTransactionType.Refund, Status = FinancialTransactionStatus.Processing,
            Amount = payment.Amount, Currency = payment.Currency, GatewayName = gateway.Name,
            IdempotencyKey = "LATE_REFUND_" + payment.Id.ToString("N"), MerchantReference = payment.MerchantReference };
        var date = payment.GatewayRequestCreatedAt ?? throw new ConcurrencyException("Thiếu thời điểm payment gốc.");
        _context.FinancialTransactions.Add(refund);
        await _context.SaveChangesAsync(ct);
        return new PreparedRefund(refund, gateway, new RefundGatewayRequest
        {
            MerchantReference = payment.MerchantReference!, GatewayReference = payment.GatewayReference,
            RequestId = refund.Id.ToString("N"), TransactionDate = date.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"),
            Amount = refund.Amount, CreatedBy = request.CreatedBy ?? "system", IpAddress = request.IpAddress ?? "127.0.0.1",
            OrderInfo = "Refund late waterbus payment", IsFullRefund = true
        }, []);
    }

    /// <summary>
    /// BR-RF-10: chỉ ghi Succeeded khi gateway XÁC NHẬN đã hoàn. Các trạng thái còn lại giữ
    /// nguyên để Admin đối soát, không tự ý ghi thành công.
    /// </summary>
    private static void ApplyGatewayOutcome(
        FinancialTransaction refund, RefundGatewayResult result, DateTimeOffset now)
    {
        switch (result.Outcome)
        {
            case RefundGatewayOutcome.Succeeded:
                refund.Status = FinancialTransactionStatus.Succeeded;
                refund.GatewayReference = result.GatewayReference;
                refund.CompletedAt = now;
                break;

            case RefundGatewayOutcome.Pending:
                refund.Status = FinancialTransactionStatus.Processing;
                refund.GatewayReference = result.GatewayReference;
                break;

            default:
                refund.Status = FinancialTransactionStatus.Requested;
                refund.FailureReason = result.FailureReason;
                refund.GatewayReference = result.GatewayReference;
                break;
        }
    }

    /// <summary>Ưu tiên gateway tự động; không có thì dùng nhánh thủ công của Admin.</summary>
    private IRefundGateway SelectGateway() =>
        _refundGateways.FirstOrDefault(g => g.SupportsAutomaticRefund)
        ?? _refundGateways.FirstOrDefault()
        ?? throw new InvalidOperationException("Chưa đăng ký IRefundGateway nào.");
}
