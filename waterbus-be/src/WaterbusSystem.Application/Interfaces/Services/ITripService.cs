using WaterbusSystem.Application.DTOs.Requests.Trips;
using WaterbusSystem.Application.DTOs.Responses.Trips;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ITripService
{
    /// <summary>Chi tiết công khai của chuyến và các điểm dừng thực sự được phục vụ.</summary>
    Task<TripDetailResponse> GetDetailAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>Sơ đồ ghế, giá phẳng và availability cho đoạn TripStopCall được chọn; không tạo hold.</summary>
    Task<TripSeatMapResponse> GetSeatMapAsync(Guid tripId, GetTripSeatMapRequest request, CancellationToken cancellationToken = default);

    Task<List<TripOperationItemResponse>> GetOperationsAsync(GetTripOperationsRequest request, CancellationToken cancellationToken = default);

    Task<List<TripOperationEventResponse>> GetOperationEventsAsync(GetTripOperationEventsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Đổi trạng thái chuyến (BR-OPS-01..04). Admin hoặc thuyền trưởng theo đúng phân quyền thao tác.</summary>
    Task<TripOperationResultResponse> ChangeStatusAsync(Guid tripId, ChangeTripStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>Thuyền trưởng báo khẩn cấp cho chuyến của tàu mình (BR-INC-04); chỉ ghi nhận, không đổi trạng thái chuyến.</summary>
    Task<SosReportResponse> ReportSosAsync(Guid tripId, ReportTripSosRequest request, CancellationToken cancellationToken = default);

    Task<List<TripSearchResponse>> SearchAsync(SearchTripsRequest request, CancellationToken cancellationToken = default);
}
