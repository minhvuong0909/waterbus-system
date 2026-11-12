using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Refunds;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.Application.Features.Refunds.Services;

/// <summary>
/// Hoàn tiền theo chuyến: Admin xem danh sách Booking cần hoàn rồi tạo yêu cầu hàng loạt. Điều kiện hoàn dùng đúng
/// <see cref="RefundEligibilityEvaluator"/> (BR-RF-01..09); việc tạo refund đi qua <see cref="IRefundRequestService"/> nên giữ nguyên
/// chống lặp, giới hạn số tiền (BR-PAY-06) và khóa giao dịch thương mại. Không chuyển tiền: refund ở Manual chỉ chờ Admin xác nhận.
/// </summary>
public sealed class RefundBatchService(IRefundCandidateRepository candidates, IRefundRequestService requests) : IRefundBatchService
{
    private const string DefaultReason = "Hoàn tiền chuyến bị hủy/chấm dứt";

    private sealed record Candidate(Booking Booking, IReadOnlyList<Ticket> Tickets, string Reason)
    {
        public decimal Amount => Tickets.Sum(t => t.PaidFareSnapshot);
    }

    public async Task<PaginatedList<RefundCandidateResponse>> ListCandidatesAsync(ListRefundCandidatesRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var all = await FindAsync(request.TripId, ct);
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).Select(c => new RefundCandidateResponse(
            c.Booking.Id, c.Booking.BookingCode, c.Booking.OrderId,
            c.Booking.Order!.PurchaserName, c.Booking.Order.PurchaserEmail, c.Booking.Order.PurchaserPhone,
            c.Amount, c.Booking.Order.Currency, c.Tickets.Select(t => t.TicketCode).ToList(), c.Reason)).ToList();
        return new PaginatedList<RefundCandidateResponse>(items, all.Count, page, pageSize);
    }

    public async Task<BatchRefundRequestResultResponse> RequestForTripAsync(BatchRequestRefundsRequest request, CancellationToken ct = default)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? DefaultReason : request.Reason.Trim();
        var created = new List<BatchRefundCreatedItem>();
        var skipped = new List<BatchRefundSkippedItem>();
        foreach (var candidate in await FindAsync(request.TripId, ct))
        {
            var booking = candidate.Booking;
            try
            {
                var result = await requests.RequestAsync(
                    new RequestRefundRequest(booking.Id, reason, request.IpAddress, request.CreatedBy), ct);
                created.Add(new BatchRefundCreatedItem(booking.Id, booking.BookingCode, result.RefundTransactionId, result.Amount, result.Status));
            }
            // Một Booking lỗi nghiệp vụ (chưa có payment, vượt số tiền còn hoàn, đã bị giữ chỗ bởi lần chạy khác...) không dừng cả lô.
            catch (ValidationException e)
            {
                skipped.Add(new(booking.Id, booking.BookingCode, string.Join(" | ", e.Errors.SelectMany(x => x.Value))));
            }
            catch (DomainException e)
            {
                skipped.Add(new(booking.Id, booking.BookingCode, e.Message));
            }
        }
        return new BatchRefundRequestResultResponse(request.TripId, created.Count, skipped.Count, created, skipped);
    }

    private async Task<IReadOnlyList<Candidate>> FindAsync(Guid tripId, CancellationToken ct)
    {
        var trip = await candidates.GetTripAsync(tripId, ct);
        // BR-RF-01/02: chỉ Trip bị hủy hoặc chấm dứt mới có căn cứ hoàn tiền.
        if (trip.Status is not (TripStatus.Cancelled or TripStatus.Terminated))
            throw new ConflictException($"Chuyến đang ở trạng thái {trip.Status}, chưa có căn cứ hoàn tiền.");

        var allocated = await candidates.GetAllocatedTicketIdsAsync(tripId, ct);
        var result = new List<Candidate>();
        foreach (var booking in await candidates.ListBookingsWithTicketsAsync(tripId, ct))
        {
            var eligible = booking.Tickets
                .Where(t => !allocated.Contains(t.Id))
                .Select(t => (Ticket: t, Decision: RefundEligibilityEvaluator.Evaluate(t, booking, trip)))
                .Where(x => x.Decision.IsEligible)
                .ToList();
            if (eligible.Count > 0)
                result.Add(new Candidate(booking, eligible.Select(x => x.Ticket).ToList(), eligible[0].Decision.Reason));
        }
        return result;
    }
}
