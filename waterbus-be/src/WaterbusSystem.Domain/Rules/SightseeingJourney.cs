using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Rules;

/// <summary>
/// Hành trình được bán của một chuyến Sightseeing (BR-ROUTE-05/06). Khác Regular, khách không tự chọn đoạn lên/xuống:
/// vé là cả hành trình tham quan đã cấu hình ở cấp Trip, từ điểm dừng ĐẦU đến điểm dừng CUỐI của chuyến theo VisitOrder.
/// Hai đầu có thể cùng một Station vật lý (chuyến vòng A → B → C → A) vì là hai TripStopCall khác VisitOrder.
/// Quy tắc thuần: không phụ thuộc EF, HTTP hay mã Station/Route cố định. Search, Seat Map, Booking và Checkout đều dùng
/// chung hàm này nên không thể có hai định nghĩa hành trình khác nhau.
/// </summary>
public static class SightseeingJourney
{
    /// <summary>Kết quả xác định hành trình; <see cref="Error"/> khác null nghĩa là cấu hình không an toàn để bán.</summary>
    public readonly record struct Result(TripStopCall? Boarding, TripStopCall? Disembarking, string? Error)
    {
        public bool IsValid => Error is null;
    }

    /// <summary>
    /// Điểm đón/trả được phép bán của chuyến. Không chọn ngầm một đoạn khi cấu hình mơ hồ: thiếu điểm dừng, trùng VisitOrder,
    /// điểm dừng đầu/cuối bị bỏ qua hoặc không cho lên (đầu)/xuống (cuối) đều bị từ chối.
    /// </summary>
    public static Result Resolve(Trip trip)
    {
        if (trip.TripType != TripType.Sightseeing)
            return new(null, null, "Chuyến này không phải chuyến Sightseeing.");

        var calls = trip.TripStopCalls.OrderBy(c => c.VisitOrder).ToList();
        if (calls.Count < 2 || calls.Any(c => c.RouteStop is null))
            return new(null, null, "Chuyến tham quan chưa khai báo đủ điểm dừng nên chưa thể mở bán.");
        if (calls.Select(c => c.VisitOrder).Distinct().Count() != calls.Count)
            return new(null, null, "Hành trình tham quan có điểm dừng trùng thứ tự nên không thể xác định điểm đón/trả.");

        var first = calls[0];
        var last = calls[^1];
        if (first.Status == "Skipped" || last.Status == "Skipped")
            return new(null, null, "Điểm đón hoặc điểm trả của chuyến tham quan không được phục vụ.");
        if (!first.RouteStop!.CanBoard)
            return new(null, null, "Điểm đầu của hành trình tham quan không cho khách lên tàu.");
        if (!last.RouteStop!.CanAlight)
            return new(null, null, "Điểm cuối của hành trình tham quan không cho khách xuống tàu.");

        return new(first, last, null);
    }

    /// <summary>Khớp cặp điểm lên/xuống khách yêu cầu với hành trình được phép; null nếu hợp lệ, ngược lại là lý do từ chối.</summary>
    public static string? Authorize(Trip trip, TripStopCall boarding, TripStopCall disembarking)
    {
        var journey = Resolve(trip);
        if (!journey.IsValid) return journey.Error;
        return boarding.Id == journey.Boarding!.Id && disembarking.Id == journey.Disembarking!.Id
            ? null
            : "Chuyến tham quan chỉ bán trọn hành trình đã cấu hình; không thể chọn điểm lên/xuống tùy ý.";
    }
}
