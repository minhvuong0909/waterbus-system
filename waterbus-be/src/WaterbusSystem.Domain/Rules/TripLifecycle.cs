using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Rules;

/// <summary>
/// Vòng đời trạng thái của Trip theo BR v2.0 mục 9 (BR-OPS-01..03):
/// <list type="bullet">
/// <item>Scheduled -> Boarding -> EnRoute -> Arrived -> Completed là luồng bình thường.</item>
/// <item>Suspended là tạm ngưng CÓ THỂ tiếp tục, không tự gây hoàn tiền.</item>
/// <item>Cancelled chỉ cho chuyến CHƯA bắt đầu phục vụ; chuyến đã bắt đầu phải kết thúc bằng Terminated.</item>
/// </list>
/// "Đã bắt đầu" được xác định bằng <see cref="Trip.ActualDepartureTime"/>, không suy từ TripStatus của một chuyến đang Suspended.
/// </summary>
public static class TripLifecycle
{
    /// <summary>
    /// Trạng thái đích khi áp dụng <paramref name="operation"/> lên <paramref name="trip"/>;
    /// null nếu thao tác không hợp lệ ở trạng thái hiện tại.
    /// </summary>
    public static TripStatus? NextStatus(Trip trip, TripOperation operation)
    {
        var started = trip.ActualDepartureTime.HasValue;

        return (trip.Status, operation) switch
        {
            (TripStatus.Scheduled, TripOperation.OpenBoarding) => TripStatus.Boarding,

            (TripStatus.Scheduled or TripStatus.Boarding, TripOperation.Start) => TripStatus.EnRoute,
            (TripStatus.EnRoute, TripOperation.Arrive) => TripStatus.Arrived,
            (TripStatus.Arrived, TripOperation.Complete) => TripStatus.Completed,

            (TripStatus.Scheduled or TripStatus.Boarding or TripStatus.EnRoute, TripOperation.Suspend) => TripStatus.Suspended,
            (TripStatus.Suspended, TripOperation.Resume) => started ? TripStatus.EnRoute : TripStatus.Scheduled,

            (TripStatus.Scheduled or TripStatus.Boarding, TripOperation.Cancel) => TripStatus.Cancelled,
            (TripStatus.Suspended, TripOperation.Cancel) when !started => TripStatus.Cancelled,

            (TripStatus.EnRoute, TripOperation.Terminate) => TripStatus.Terminated,
            (TripStatus.Suspended, TripOperation.Terminate) when started => TripStatus.Terminated,

            _ => null
        };
    }

    /// <summary>Các thao tác (đổi trạng thái) đang hợp lệ cho chuyến, theo thứ tự ổn định.</summary>
    public static IReadOnlyList<TripOperation> AllowedOperations(Trip trip) =>
        Enum.GetValues<TripOperation>()
            .Where(op => op != TripOperation.NoAction && NextStatus(trip, op) is not null)
            .ToList();

    /// <summary>Thao tác Captain được phép tự thực hiện trên chuyến của tàu mình (BR-OPS-04: Start, Arrive).</summary>
    public static bool IsCaptainOperation(TripOperation operation) =>
        operation is TripOperation.OpenBoarding or TripOperation.Start or TripOperation.Arrive;

    /// <summary>Thao tác bắt buộc có lý do để lưu vết quyết định.</summary>
    public static bool RequiresReason(TripOperation operation) =>
        operation is TripOperation.Suspend or TripOperation.Cancel or TripOperation.Terminate or TripOperation.NoAction;

    /// <summary>Chuỗi ghi vào IncidentTrip.OperationDecision khi Admin quyết định cho chuyến.</summary>
    public static string DecisionOf(TripOperation operation) =>
        operation == TripOperation.NoAction ? "None" : operation.ToString();
}
