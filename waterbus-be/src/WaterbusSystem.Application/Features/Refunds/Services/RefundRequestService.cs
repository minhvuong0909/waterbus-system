using FluentValidation;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Refunds.Services;

public sealed class RefundRequestService : IRefundRequestService
{
    private readonly IFinancialTransactionRepository _refunds;
    private readonly IBookingRepository _bookings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RequestRefundRequest> _validator;
    private readonly IEnumerable<IRefundGateway> _refundGateways;
    private readonly ICommercialTransactionRunner _transactions;

    public RefundRequestService(IFinancialTransactionRepository refunds, IBookingRepository bookings, IUnitOfWork unitOfWork, IValidator<RequestRefundRequest> validator, IEnumerable<IRefundGateway> refundGateways, ICommercialTransactionRunner transactions)
    {
        _refunds = refunds; _bookings = bookings; _unitOfWork = unitOfWork; _validator = validator;
        _refundGateways = refundGateways;
        _transactions = transactions;
    }

    public async Task<RequestRefundResultDto> RequestAsync(RequestRefundRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);
        var prepared = await _transactions.RunAsync(() => PrepareAsync(request, cancellationToken), cancellationToken);
        if (prepared.Request is not null)
        {
            RefundGatewayResult outcome;
            try { outcome = await prepared.Gateway.RefundAsync(prepared.Request, cancellationToken); }
            catch (Exception) { outcome = RefundGatewayResult.Pending(); }
            await _transactions.RunAsync(async () =>
            {
                var current = await _refunds.GetRefundForUpdateAsync(prepared.Refund.Id, cancellationToken);
                if (current.Status != FinancialTransactionStatus.Processing) return true;
                ApplyGatewayOutcome(current, outcome, DateTimeOffset.UtcNow);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
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

    private async Task<PreparedRefund> PrepareAsync(RequestRefundRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var booking = await _bookings.GetForRefundAsync(request.BookingId, cancellationToken);

        if (booking.Order == null || booking.Trip == null)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundRequest.BookingId), "Booking chưa gắn đủ Order hoặc Trip nên không thể hoàn tiền.")
            });
        }

        var existing = await _refunds.GetActiveRefundForBookingAsync(booking.Id, cancellationToken);
        if (existing is not null)
            return new PreparedRefund(existing, SelectGateway(), null, existing.RefundAllocations
                .Select(a => new RefundTicketAllocationDto(a.TicketId, a.Ticket!.TicketCode, a.Amount, "Existing refund")).ToList());

        var trip = booking.Trip;
        var payment = await _refunds.GetSuccessfulPaymentForRefundAsync(booking.OrderId, booking.Tickets.Count > 0, cancellationToken);

        if (payment == null)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundRequest.BookingId),
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
                new(nameof(RequestRefundRequest.BookingId), "Booking chưa được phát hành vé nên không có gì để hoàn.")
            });
        }
        var decisions = tickets
            .Select(t => (Ticket: t, Decision: RefundEligibilityEvaluator.Evaluate(t, booking, trip)))
            .ToList();

        var eligible = decisions.Where(d => d.Decision.IsEligible).ToList();
        if (eligible.Count == 0)
        {
            var reasons = string.Join(" | ", decisions.Select(d => $"{d.Ticket.TicketCode}: {d.Decision.Reason}"));
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundRequest.BookingId), $"Không có vé nào đủ điều kiện hoàn tiền. {reasons}")
            });
        }
        var refundableAmount = 0m;

        var alreadyRefundedTicketIds = await _refunds.GetAllocatedRefundTicketIdsAsync(booking.OrderId, cancellationToken);

        var refundableTickets = eligible
            .Where(d => !alreadyRefundedTicketIds.Contains(d.Ticket.Id))
            .ToList();

        if (refundableTickets.Count == 0)
        {
            throw new ConcurrencyException("Các vé này đã có yêu cầu hoàn tiền trước đó. Không được hoàn trùng.");
        }

        refundableAmount = refundableTickets.Sum(d => d.Ticket.PaidFareSnapshot);
        var settledRefundTotal = await _refunds.GetCommittedRefundTotalAsync(payment.Id, cancellationToken);

        var refundableBalance = payment.Amount - settledRefundTotal;
        if (refundableAmount > refundableBalance)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(RequestRefundRequest.BookingId),
                    $"Số tiền cần hoàn {refundableAmount:N0} vượt quá số tiền còn có thể hoàn {refundableBalance:N0}.")
            });
        }

        var gateway = SelectGateway();
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
                Amount = item.Ticket.PaidFareSnapshot
            });

            allocations.Add(new RefundTicketAllocationDto(
                item.Ticket.Id, item.Ticket.TicketCode, item.Ticket.PaidFareSnapshot, item.Decision.Reason));
            item.Ticket.Status = terminalTicketStatus;
        }
        refund.ValidateRefund(payment, booking, refundableBalance);
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
                IpAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? "127.0.0.1" : request.IpAddress,
                OrderInfo = "Refund waterbus " + refund.Id.ToString("N"),
                IsFullRefund = refundableAmount == payment.Amount
            };
        _refunds.Add(refund);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new PreparedRefund(refund, gateway, gatewayRequest, allocations);
    }

    private async Task<PreparedRefund> PrepareLatePaymentRefundAsync(Booking booking, FinancialTransaction payment,
        RequestRefundRequest request, CancellationToken ct)
    {
        if (await _bookings.HasTicketsForOrderAsync(booking.OrderId, ct))
            throw new ConcurrencyException("Khoản payment muộn có vé cần đối soát riêng.");
        var existing = await _refunds.GetRefundForPaymentAsync(payment.Id, ct);
        var gateway = SelectGateway();
        if (existing is not null) return new PreparedRefund(existing, gateway, null, []);
        var refund = new FinancialTransaction { Order = booking.Order, OrderId = booking.OrderId,
            RefundBooking = booking, RefundBookingId = booking.Id, OriginalPayment = payment, OriginalPaymentId = payment.Id,
            TransactionType = FinancialTransactionType.Refund, Status = FinancialTransactionStatus.Processing,
            Amount = payment.Amount, Currency = payment.Currency, GatewayName = gateway.Name,
            IdempotencyKey = "LATE_REFUND_" + payment.Id.ToString("N"), MerchantReference = payment.MerchantReference };
        var date = payment.GatewayRequestCreatedAt ?? throw new ConcurrencyException("Thiếu thời điểm payment gốc.");
        _refunds.Add(refund);
        await _unitOfWork.SaveChangesAsync(ct);
        return new PreparedRefund(refund, gateway, new RefundGatewayRequest
        {
            MerchantReference = payment.MerchantReference!, GatewayReference = payment.GatewayReference,
            RequestId = refund.Id.ToString("N"), TransactionDate = date.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"),
            Amount = refund.Amount, CreatedBy = request.CreatedBy ?? "system", IpAddress = request.IpAddress ?? "127.0.0.1",
            OrderInfo = "Refund late waterbus payment", IsFullRefund = true
        }, []);
    }
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
    private IRefundGateway SelectGateway() =>
        _refundGateways.FirstOrDefault(g => g.SupportsAutomaticRefund)
        ?? _refundGateways.FirstOrDefault()
        ?? throw new InvalidOperationException("Chưa đăng ký IRefundGateway nào.");
}
