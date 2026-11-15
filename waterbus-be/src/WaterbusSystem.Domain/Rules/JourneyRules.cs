using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Rules;

/// <summary>
/// Quy tắc chọn bến lên và bến xuống của một hành trình (BR-ROUTE-06).
/// Chuyến du lịch (Sightseeing) dạng vòng được lên và xuống cùng một Station vật lý, miễn là hai lần ghé có VisitOrder
/// khác nhau và hợp lệ. Chuyến thường (Commuter) thì không: khách không được chọn cùng một Station làm cả bến lên và
/// bến xuống, kể cả khi tuyến có lặp bến. Không áp <c>BoardingStationId != DisembarkingStationId</c> cho mọi chuyến.
/// Dùng chung cho đặt vé, giữ chỗ checkout, sơ đồ ghế và tìm kiếm để các nơi không lệch nhau.
/// </summary>
public static class JourneyRules
{
    public static bool AllowsSameStation(TripType tripType) => tripType == TripType.Sightseeing;

    /// <summary>True khi cặp bến này bị cấm cho loại chuyến: cùng một Station mà chuyến không phải Sightseeing.</summary>
    public static bool IsForbiddenSameStation(TripType tripType, Guid boardingStationId, Guid disembarkingStationId) =>
        boardingStationId == disembarkingStationId && !AllowsSameStation(tripType);
}
