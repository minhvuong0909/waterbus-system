using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services;

/// <summary>
/// Tính khoảng cách địa lý bằng công thức Haversine trên mặt cầu.
/// Phù hợp cho khoảng cách ngắn của geofence POI trên sông. Đây là xấp xỉ
/// mặt cầu; sai số so với ellipsoid WGS-84 cần được tính đến khi chọn bán kính.
///
/// Roadmap repo nêu NetTopologySuite; Haversine được chọn cho core vì
/// không cần thêm dependency và đủ chính xác cho use case geofence.
/// Nếu nhóm yêu cầu NetTopologySuite, có thể tạo adapter khác
/// implement IGeoDistanceCalculator mà không đổi Application/test.
/// </summary>
public sealed class HaversineDistanceCalculator : IGeoDistanceCalculator
{
    /// <summary>Bán kính trung bình Trái Đất (mét).</summary>
    private const double EarthRadiusMeters = 6_371_000.0;

    /// <inheritdoc />
    public double CalculateDistanceMeters(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        ValidateCoordinate(lat1, lng1, nameof(lat1), nameof(lng1));
        ValidateCoordinate(lat2, lng2, nameof(lat2), nameof(lng2));

        double lat1Rad = DegreesToRadians((double)lat1);
        double lat2Rad = DegreesToRadians((double)lat2);
        double deltaLat = DegreesToRadians((double)(lat2 - lat1));
        double deltaLng = DegreesToRadians((double)(lng2 - lng1));

        // Haversine formula
        double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                 + Math.Cos(lat1Rad) * Math.Cos(lat2Rad)
                 * Math.Sin(deltaLng / 2) * Math.Sin(deltaLng / 2);

        // Sai số làm tròn có thể đưa a hơi ra ngoài [0, 1] ở các điểm gần đối cực.
        a = Math.Clamp(a, 0.0, 1.0);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    /// <inheritdoc />
    public bool IsWithinTriggerRadius(
        decimal boatLat, decimal boatLng,
        decimal? poiLat, decimal? poiLng,
        int? triggerRadiusMeters)
    {
        // GPS sai phải được báo lỗi kể cả khi POI/radius chưa được cấu hình.
        ValidateCoordinate(boatLat, boatLng, nameof(boatLat), nameof(boatLng));

        if (triggerRadiusMeters is <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(triggerRadiusMeters),
                triggerRadiusMeters.Value,
                "Bán kính kích hoạt phải là số dương (mét).");

        // POI chưa có tọa độ → không thể xác định → false
        if (!poiLat.HasValue || !poiLng.HasValue)
            return false;

        // Bán kính null → không kích hoạt (không tự đặt default)
        if (!triggerRadiusMeters.HasValue)
            return false;

        // POI đã có tọa độ thì phải hợp lệ.
        ValidateCoordinate(poiLat.Value, poiLng.Value, nameof(poiLat), nameof(poiLng));

        double distance = CalculateDistanceMeters(boatLat, boatLng, poiLat.Value, poiLng.Value);

        return distance <= triggerRadiusMeters.Value;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);

    private static void ValidateCoordinate(
        decimal latitude, decimal longitude,
        string latParamName, string lngParamName)
    {
        if (latitude < -90m || latitude > 90m)
            throw new ArgumentOutOfRangeException(
                latParamName,
                latitude,
                "Vĩ độ phải nằm trong khoảng [-90, 90].");

        if (longitude < -180m || longitude > 180m)
            throw new ArgumentOutOfRangeException(
                lngParamName,
                longitude,
                "Kinh độ phải nằm trong khoảng [-180, 180].");
    }
}
