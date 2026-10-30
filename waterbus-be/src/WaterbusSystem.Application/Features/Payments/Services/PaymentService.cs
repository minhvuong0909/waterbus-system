using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.Payments;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Payments.Services;

public sealed class PaymentService(IOrderRepository orders, IFinancialTransactionRepository payments,
    IBookingRepository bookings, IUnitOfWork unitOfWork, IVnPayService vnPay,
    ICommercialTransactionRunner transactions, CheckoutOptions options, ICheckoutAccessService access,
    PaymentConfirmationService confirmation, ICheckoutSessionService? sessions = null) : IPaymentService
{
    public async Task<CreateVnPayUrlResponseDto> CreateVnPayUrlAsync(Guid orderId, string ipAddress, string? accessToken, CancellationToken ct = default)
    {
        await access.RequireAsync(orderId, accessToken, ct);
        var result = await transactions.RunAsync(async () =>
        {
            var order = await orders.GetForPaymentAsync(orderId, ct);
            var now = DateTimeOffset.UtcNow;
            if (order.Status != OrderStatus.PendingPayment || order.ExpiresAt <= now || order.ExpiresAt == null ||
                order.Bookings.Any(b => b.Status != BookingStatus.PendingPayment ||
                    b.Trip == null || b.Trip.Status is not (TripStatus.Scheduled or TripStatus.Boarding) || b.Trip.ActualDepartureTime != null) ||
                await bookings.HasUnavailableInventoryAsync(orderId, ct))
                throw new ConcurrencyException("Đơn hàng đã hết hạn hoặc không còn chờ thanh toán.");
            var attempt = await payments.GetRequestedPaymentAsync(order.Id, ct);
            if (attempt is null)
            {
                if (!await payments.HasPaymentAsync(order.Id, ct))
                    order.ExpiresAt = now.Add(options.PaymentDuration);
                attempt = new FinancialTransaction { Order = order, OrderId = order.Id, Amount = order.QuotedTotal,
                    Currency = order.Currency, GatewayName = "VNPAY", TransactionType = FinancialTransactionType.Payment,
                    GatewayRequestCreatedAt = now, Status = FinancialTransactionStatus.Requested };
                attempt.MerchantReference = attempt.Id.ToString("N");
                attempt.IdempotencyKey = "PAY_" + attempt.Id.ToString("N");
                payments.Add(attempt);
            }
            var url = vnPay.CreatePaymentUrl(attempt.MerchantReference!, attempt.Amount,
                "Thanh toan waterbus", ipAddress, attempt.GatewayRequestCreatedAt, order.ExpiresAt);
            await unitOfWork.SaveChangesAsync(ct);
            return new CreateVnPayUrlResponseDto(url, order.Id.ToString(), attempt.MerchantReference!, attempt.Amount, order.ExpiresAt!.Value);
        }, ct);
        if (sessions is not null)
            try
            {
                var sessionId = "order:" + orderId;
                if (await sessions.GetAsync(sessionId, ct) is null)
                {
                    var reservations = await bookings.GetPendingReservationsByOrderAsync(orderId, ct);
                    var holds = reservations.Select(r => new SeatHold(r.TripId, r.SeatId, r.BoardingStopOrder, r.DisembarkingStopOrder)).ToList();
                    await sessions.HoldUntilAsync(sessionId, orderId, holds, result.ExpiresAt, ct);
                }
                await sessions.MarkPaymentPendingUntilAsync(sessionId, orderId, result.ExpiresAt, ct);
            }
            catch (Exception) {  }
        return result;
    }
    public async Task<VnPayIpnResultDto> ReconcileAsync(Guid orderId, Guid paymentId, string? accessToken, string ipAddress, CancellationToken ct = default)
    {
        await access.RequireAsync(orderId, accessToken, ct);
        var attempt = await payments.GetPaymentSnapshotAsync(orderId, paymentId, ct);
        var date = attempt.GatewayRequestCreatedAt ?? throw new ConcurrencyException("Không có thời điểm request gốc để đối soát.");
        var result = await vnPay.QueryTransactionAsync(attempt.MerchantReference!, date.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"), ipAddress, ct);
        if (!result.IsSuccess || result.MerchantReference != attempt.MerchantReference || result.Amount != attempt.Amount * 100 || result.TransactionType != "01")
            return new("99", "Reconciliation pending");
        return await confirmation.ConfirmVerifiedAsync(new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = "", ["vnp_TxnRef"] = attempt.MerchantReference!,
            ["vnp_Amount"] = ((long)(attempt.Amount * 100)).ToString(),
            ["vnp_TransactionNo"] = result.TransactionNo ?? "", ["vnp_TransactionStatus"] = result.TransactionStatus ?? "",
            ["vnp_ResponseCode"] = result.TransactionStatus == "00" ? "00" : "99"
        }, ct);
    }

    public Task<VnPayIpnResultDto> ConfirmIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default) =>
        confirmation.ConfirmIpnAsync(query, ct);

    public Task<VnPayReturnDto> GetReturnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default)
    {
        var valid = vnPay.ValidateSignature(query) && vnPay.IsValidTmnCode(query.GetValueOrDefault("vnp_TmnCode", ""));
        var code = query.GetValueOrDefault("vnp_ResponseCode", "");
        var success = valid && code == "00" && query.GetValueOrDefault("vnp_TransactionStatus") == "00";
        return Task.FromResult(new VnPayReturnDto(success, query.GetValueOrDefault("vnp_TxnRef", ""), code,
            valid ? "Kết quả chuyển hướng; xem trạng thái Order được backend xác minh." : "Chữ ký chuyển hướng không hợp lệ."));
    }
}
