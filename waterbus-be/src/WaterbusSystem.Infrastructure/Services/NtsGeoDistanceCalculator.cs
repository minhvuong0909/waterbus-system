using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services;

/// <summary>
/// Chiếu WGS84 (kinh/vĩ độ) sang cùng UTM zone, sau đó dùng
/// NetTopologySuite tính khoảng cách phẳng theo mét.
/// Chỉ dùng cho các cặp Boat–POI gần nhau trong geofence; UTM không phải
/// công thức khoảng cách toàn cầu cho các điểm cách xa nhau.
/// </summary>
public sealed class NtsGeoDistanceCalculator : IGeoDistanceCalculator
{
    public double CalculateDistanceMeters(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        ValidateCoordinate(lat1, lng1, nameof(lat1), nameof(lng1));
        ValidateCoordinate(lat2, lng2, nameof(lat2), nameof(lng2));

        // Cả hai điểm phải dùng cùng projection. Chọn zone theo trung điểm,
        // kể cả khi hai kinh độ nằm hai bên đường đổi ngày.
        double longitude1 = (double)lng1;
        double longitude2 = (double)lng2;
        if (longitude2 - longitude1 > 180.0) longitude2 -= 360.0;
        if (longitude1 - longitude2 > 180.0) longitude2 += 360.0;
        double midLongitude = (longitude1 + longitude2) / 2.0;
        if (midLongitude < -180.0) midLongitude += 360.0;
        if (midLongitude > 180.0) midLongitude -= 360.0;

        int zone = Math.Clamp((int)Math.Floor((midLongitude + 180.0) / 6.0) + 1, 1, 60);
        bool northernHemisphere = ((double)lat1 + (double)lat2) / 2.0 >= 0;
        var utm = ProjectedCoordinateSystem.WGS84_UTM(zone, northernHemisphere);
        var transform = new CoordinateTransformationFactory()
            .CreateFromCoordinateSystems(GeographicCoordinateSystem.WGS84, utm)
            .MathTransform;

        // ProjNET nhận [longitude, latitude]; UTM trả [easting, northing] mét.
        var first = transform.Transform(new[] { (double)lng1, (double)lat1 });
        var second = transform.Transform(new[] { (double)lng2, (double)lat2 });
        int srid = (northernHemisphere ? 32600 : 32700) + zone;
        var factory = new GeometryFactory(new PrecisionModel(), srid);
        var firstPoint = factory.CreatePoint(new Coordinate(first[0], first[1]));
        var secondPoint = factory.CreatePoint(new Coordinate(second[0], second[1]));

        return firstPoint.Distance(secondPoint);
    }

    public bool IsWithinTriggerRadius(
        decimal boatLat, decimal boatLng,
        decimal? poiLat, decimal? poiLng,
        int? triggerRadiusMeters)
    {
        ValidateCoordinate(boatLat, boatLng, nameof(boatLat), nameof(boatLng));

        if (triggerRadiusMeters is <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(triggerRadiusMeters), triggerRadiusMeters.Value,
                "Bán kính kích hoạt phải là số dương (mét).");

        if (!poiLat.HasValue || !poiLng.HasValue || !triggerRadiusMeters.HasValue)
            return false;

        ValidateCoordinate(poiLat.Value, poiLng.Value, nameof(poiLat), nameof(poiLng));
        return CalculateDistanceMeters(boatLat, boatLng, poiLat.Value, poiLng.Value)
            <= triggerRadiusMeters.Value;
    }

    private static void ValidateCoordinate(
        decimal latitude, decimal longitude,
        string latParamName, string lngParamName)
    {
        if (latitude < -90m || latitude > 90m)
            throw new ArgumentOutOfRangeException(latParamName, latitude,
                "Vĩ độ phải nằm trong khoảng [-90, 90].");
        if (longitude < -180m || longitude > 180m)
            throw new ArgumentOutOfRangeException(lngParamName, longitude,
                "Kinh độ phải nằm trong khoảng [-180, 180].");
    }
}
