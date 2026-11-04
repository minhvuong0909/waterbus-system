using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface ITripRepository
{
    /// <summary>
    /// Chuyến có trạng thái trong <paramref name="statuses"/>, khởi hành trước <paramref name="horizon"/>
    /// và chưa đến bến tại <paramref name="now"/>.
    /// </summary>
    Task<List<ActiveTripRef>> ListActiveInWindowAsync(
        IReadOnlyCollection<TripStatus> statuses, DateTimeOffset now, DateTimeOffset horizon, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyến mới nhất (theo giờ khởi hành) của tàu có trạng thái trong <paramref name="statuses"/>; null nếu không có.
    /// </summary>
    Task<Guid?> FindLatestTripIdByBoatAsync(
        Guid boatId, IReadOnlyCollection<TripStatus> statuses, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyến (chỉ đọc) kèm Route, Boat, các điểm dừng thực sự (TripStopCall → RouteStop → Station, loại
    /// bỏ bản ghi xóa mềm/mất dữ liệu danh mục) hoặc ném NotFoundException. Dùng cho GetDetail/GetSeatMap.
    /// </summary>
    Task<Trip> GetDetailGraphAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>Ghế của 1 tàu (kèm SeatClass), sắp theo hàng/cột/mã ghế.</summary>
    Task<List<Seat>> ListSeatsForBoatAsync(Guid boatId, CancellationToken cancellationToken = default);

    /// <summary>Bảng giá đang hiệu lực tại <paramref name="now"/> cho 1 loại chuyến.</summary>
    Task<List<FareRule>> ListActiveFaresForTypeAsync(string tripType, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>SeatReservation (chưa Cancelled) chạm đoạn [boardingVisitOrder, disembarkingVisitOrder) của 1 chuyến.</summary>
    Task<List<SeatReservationOverlap>> ListOverlappingReservationsAsync(
        Guid tripId, int boardingVisitOrder, int disembarkingVisitOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyến cho màn hình vận hành (kèm Route, Boat), lọc theo khoảng thời gian/thuyền trưởng/trạng thái,
    /// sắp theo giờ khởi hành tăng dần, giới hạn <paramref name="take"/> bản ghi.
    /// </summary>
    Task<List<Trip>> ListForOperationsAsync(
        DateTimeOffset from, DateTimeOffset to, Guid? captainId, IReadOnlyList<TripStatus>? statuses, int take,
        CancellationToken cancellationToken = default);

    /// <summary>Nhật ký thao tác vận hành, mới nhất trước; có thể lọc theo 1 chuyến, giới hạn <paramref name="take"/>.</summary>
    Task<List<TripOperationEvent>> ListOperationEventsAsync(
        Guid? tripId, int take, CancellationToken cancellationToken = default);

    /// <summary>Chuyến (có theo dõi để sửa) kèm Boat hoặc ném NotFoundException. Dùng cho ChangeStatus.</summary>
    Task<Trip> GetTrackedWithBoatAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>Chuyến (chỉ đọc) kèm Boat hoặc ném NotFoundException. Dùng cho ReportSos.</summary>
    Task<Trip> GetWithBoatAsync(Guid tripId, CancellationToken cancellationToken = default);

    void AddOperationEvent(TripOperationEvent operationEvent);

    /// <summary>
    /// Chuyến đang Scheduled (kèm Route, Boat, các điểm dừng thực sự), lọc theo ngày khởi hành (giờ VN,
    /// đã quy đổi UTC) và loại chuyến, sắp theo giờ khởi hành tăng dần.
    /// </summary>
    Task<List<Trip>> SearchScheduledAsync(
        DateTimeOffset? departureDayStartUtc, DateTimeOffset? departureDayEndUtc, TripType? tripType,
        CancellationToken cancellationToken = default);

    /// <summary>SeatReservation (chưa Cancelled) của nhiều chuyến, dùng đếm ghế bận theo đoạn khi tìm kiếm.</summary>
    Task<List<SeatReservationSegment>> ListReservationSegmentsAsync(
        IReadOnlyList<Guid> tripIds, CancellationToken cancellationToken = default);

    /// <summary>Giá thấp nhất đang hiệu lực tại <paramref name="now"/>, theo từng loại chuyến.</summary>
    Task<Dictionary<string, decimal>> GetMinActivePriceByTripTypeAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
