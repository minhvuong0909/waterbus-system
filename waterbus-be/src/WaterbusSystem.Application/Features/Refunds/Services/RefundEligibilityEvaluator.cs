using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Refunds.Services;

/// <summary>Kết quả đánh giá một Ticket có đủ điều kiện hoàn tiền hay không, kèm lý do để đối soát.</summary>
public sealed record RefundDecision(bool IsEligible, string Reason)
{
    public static RefundDecision Eligible(string reason) => new(true, reason);
    public static RefundDecision NotEligible(string reason) => new(false, reason);
}

/// <summary>
/// BR-RF-01 → BR-RF-10: quyết định Ticket nào thuộc "chặng chưa được phục vụ" và do đó được hoàn 100%
/// số tiền đã thanh toán phân bổ cho Ticket đó.
///
/// Nguyên tắc xuyên suốt (không phân biệt tỷ lệ, không prorate):
///   • NoShow do khách bỏ lỡ check-in  -> KHÔNG hoàn.
///   • Đã phục vụ tới đúng bến xuống dự kiến -> KHÔNG hoàn, coi là đã hoàn tất.
///   • Chưa được phục vụ, hoặc bị gián đoạn giữa chừng -> hoàn 100% paid allocation.
/// Không có Voucher/Wallet/compensation bù (BR-RF-05).
/// </summary>
public static class RefundEligibilityEvaluator
{
    public static RefundDecision Evaluate(Ticket ticket, Booking booking, Trip trip, DateTimeOffset now)
    {
        // BR-STATE-02 / BR-RF-01: chỉ Trip bị hủy hoặc chấm dứng mới là căn cứ hoàn tiền.
        if (trip.Status is not (TripStatus.Cancelled or TripStatus.Terminated))
        {
            return RefundDecision.NotEligible(
                $"Chuyến tàu đang ở trạng thái {trip.Status}, chưa có căn cứ hoàn tiền.");
        }

        // BR-RF-06 / BR-RF-09: NoShow do khách không đến hoặc không check-in đúng hạn thì không hoàn.
        // Lưu ý BR-RF-06 cũng yêu cầu KHÔNG đánh dấu NoShow cho hành khách chưa có cơ hội boarding
        // vì Trip bị hủy/chấm dứng trước khi tới bến lên - điều đó do luồng check-in quyết định,
        // không phải ở đây.
        if (ticket.BoardingStatus == BoardingStatus.NoShow)
        {
            return RefundDecision.NotEligible("Hành khách không check-in (NoShow) nên không được hoàn tiền.");
        }

        if (ticket.Status is TicketStatus.Completed)
        {
            return RefundDecision.NotEligible("Vé đã hoàn tất hành trình.");
        }

        // BR-RF-03: Booking đã được phục vụ đến đúng bến xuống dự kiến trước khi chuyến gặp sự cố
        // thì coi là đã hoàn tất, không refund vì sự cố xảy ra sau đó.
        if (booking.Status == BookingStatus.Completed)
        {
            return RefundDecision.NotEligible("Booking đã được phục vụ đến đúng bến xuống và đã hoàn tất.");
        }

        if (WasServed(booking.DisembarkingCall))
        {
            return RefundDecision.NotEligible(
                "Đoạn đi đã được phục vụ đến đúng bến xuống dự kiến trước khi chuyến gặp sự cố.");
        }

        if (ticket.Status is TicketStatus.Cancelled or TicketStatus.Terminated)
        {
            return RefundDecision.NotEligible("Vé đã bị vô hiệu hoá từ trước nên không phát sinh khoản hoàn mới.");
        }

        // BR-RF-01 (Cancelled trước khi bắt đầu) và BR-RF-04 (chưa được phục vụ do Trip bị dừng
        // hoặc đã bắt đầu nhưng bị cắt giữa chừng): cùng một xử lý - hoàn 100% paid allocation.
        var reason = trip.Status == TripStatus.Cancelled
            ? "Chuyến bị hủy trước khi phục vụ đoạn đi này."
            : "Chuyến bị chấm dứng giữa hành trình, đoạn đi chưa được phục vụ đến bến xuống dự kiến.";

        return RefundDecision.Eligible(reason);
    }

    /// <summary>
    /// BR-RF-03: coi như đã phục vụ khi tàu đã tới bến xuống dự kiến (hoặc đã rời bến đó).
    /// Dùng dữ liệu thực tế tại TripStopCall thay vì suy luận từ TripStatus toàn tuyến (BR-STOP-02).
    /// </summary>
    private static bool WasServed(TripStopCall? disembarkingCall) =>
        disembarkingCall is not null
        && (disembarkingCall.ActualArrivalTime is not null
            || disembarkingCall.ActualDepartureTime is not null
            || disembarkingCall.Status is "Arrived" or "Departed");
}