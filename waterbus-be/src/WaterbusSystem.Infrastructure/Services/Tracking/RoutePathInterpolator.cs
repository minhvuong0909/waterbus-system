namespace WaterbusSystem.Infrastructure.Services.Tracking;

/// <summary>
/// Nội suy vị trí dọc đường gấp khúc nối các bến theo thứ tự ghé (dùng cho giả lập GPS).
/// Khoảng cách ngắn trong nội thành nên dùng xấp xỉ equirectangular là đủ chính xác.
/// </summary>
public static class RoutePathInterpolator
{
    private const double EarthRadiusKm = 6371.0;
    private const double KmPerNauticalMile = 1.852;

    public readonly record struct GeoPoint(double Latitude, double Longitude);

    /// <summary>
    /// Vị trí tại tỉ lệ <paramref name="fraction"/> (0..1) của tổng chiều dài đường đi.
    /// </summary>
    public static GeoPoint PointAt(IReadOnlyList<GeoPoint> path, double fraction)
    {
        if (path.Count == 0) throw new ArgumentException("Đường đi phải có ít nhất 1 điểm.", nameof(path));
        if (path.Count == 1) return path[0];

        fraction = Math.Clamp(fraction, 0, 1);
        var total = TotalLengthKm(path);
        if (total <= 0) return path[0];

        var target = total * fraction;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var segment = DistanceKm(path[i], path[i + 1]);
            if (target <= segment || i == path.Count - 2)
            {
                var t = segment <= 0 ? 0 : Math.Clamp(target / segment, 0, 1);
                return new GeoPoint(
                    path[i].Latitude + (path[i + 1].Latitude - path[i].Latitude) * t,
                    path[i].Longitude + (path[i + 1].Longitude - path[i].Longitude) * t);
            }
            target -= segment;
        }

        return path[^1];
    }

    public static double TotalLengthKm(IReadOnlyList<GeoPoint> path)
    {
        double total = 0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            total += DistanceKm(path[i], path[i + 1]);
        }
        return total;
    }

    /// <summary>Tốc độ trung bình (hải lý/giờ) để đi hết đường trong khoảng thời gian cho trước.</summary>
    public static double AverageSpeedKnots(IReadOnlyList<GeoPoint> path, TimeSpan duration) =>
        duration <= TimeSpan.Zero ? 0 : TotalLengthKm(path) / KmPerNauticalMile / duration.TotalHours;

    public static double DistanceKm(GeoPoint a, GeoPoint b)
    {
        var lat1 = DegreesToRadians(a.Latitude);
        var lat2 = DegreesToRadians(b.Latitude);
        var x = DegreesToRadians(b.Longitude - a.Longitude) * Math.Cos((lat1 + lat2) / 2);
        var y = lat2 - lat1;
        return Math.Sqrt(x * x + y * y) * EarthRadiusKm;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
