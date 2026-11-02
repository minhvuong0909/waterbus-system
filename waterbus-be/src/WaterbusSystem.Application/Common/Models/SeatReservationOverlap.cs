using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Common.Models;

/// <summary>SeatReservation chạm đoạn [boarding, disembarking) đang xét của 1 chuyến, dùng cho sơ đồ ghế.</summary>
public record SeatReservationOverlap(Guid SeatId, ReservationStatus Status, DateTimeOffset? ExpiresAt);

/// <summary>SeatReservation (chưa Cancelled) của nhiều chuyến, dùng đếm ghế bận theo đoạn khi tìm kiếm.</summary>
public record SeatReservationSegment(Guid TripId, Guid SeatId, int BoardingStopOrder, int DisembarkingStopOrder);
