using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Rules;

namespace WaterbusSystem.Application.Features.Trips;

/// <summary>Trạng thái của một nút trên màn hình thuyền trưởng. <c>Reason</c> là mã (NotYet, OpenBoardingFirst, NoPosition, NotAtLastStop, JourneyNotDone, WrongStatus).</summary>
public sealed record CaptainActionHint(string Operation, bool Allowed, string? Reason, DateTimeOffset? AvailableAt);

/// <summary>
/// Khi nào thuyền trưởng được bấm nút nào (BR-OPS-04), chồng lên vòng đời trạng thái <see cref="TripLifecycle"/>:
/// Mở đón khách (từ N phút trước giờ khởi hành) → Bắt đầu (chỉ từ Boarding) → Đã cập bến (chỉ khi tàu ở gần bến cuối) →
/// Kết thúc chuyến (khi đã cập bến). Với tour vòng (bến đầu cũng là bến cuối) tàu phải đã đi xa bến cuối rồi mới quay lại.
/// Thuần logic, không truy cập dữ liệu.
/// </summary>
public static class CaptainOperationRules
{
    public static readonly TripOperation[] Operations =
        [TripOperation.OpenBoarding, TripOperation.Start, TripOperation.Arrive, TripOperation.Complete];

    /// <param name="distanceToLastStopMeters">Khoảng cách tàu tới bến cuối; null nếu chưa có vị trí đủ mới.</param>
    /// <param name="hasLeftLastStop">Tàu đã từng đi xa bến cuối trong chuyến này (null = chưa xác định, không chặn).</param>
    public static IReadOnlyList<CaptainActionHint> Evaluate(
        Trip trip, DateTimeOffset now, double? distanceToLastStopMeters, CaptainOperationOptions options, bool? hasLeftLastStop = null) =>
        Operations.Select(op => Evaluate(trip, op, now, distanceToLastStopMeters, options, hasLeftLastStop)).ToList();

    public static CaptainActionHint Evaluate(
        Trip trip, TripOperation operation, DateTimeOffset now, double? distanceToLastStopMeters, CaptainOperationOptions options,
        bool? hasLeftLastStop = null)
    {
        var name = operation.ToString();
        if (TripLifecycle.NextStatus(trip, operation) is null)
            return new(name, false, "WrongStatus", null);
        if (!options.Enforce) return new(name, true, null, null);

        switch (operation)
        {
            case TripOperation.OpenBoarding:
                var opensAt = trip.DepartureTime.AddMinutes(-Math.Max(0, options.OpenBoardingLeadMinutes));
                return now < opensAt ? new(name, false, "NotYet", opensAt) : new(name, true, null, null);

            case TripOperation.Start when trip.Status == TripStatus.Scheduled:
                return new(name, false, "OpenBoardingFirst", null);

            case TripOperation.Arrive:
                if (distanceToLastStopMeters is null) return new(name, false, "NoPosition", null);
                if (distanceToLastStopMeters > options.ArriveRadiusM) return new(name, false, "NotAtLastStop", null);
                return hasLeftLastStop == false ? new(name, false, "JourneyNotDone", null) : new(name, true, null, null);

            default:
                return new(name, true, null, null);
        }
    }
}
