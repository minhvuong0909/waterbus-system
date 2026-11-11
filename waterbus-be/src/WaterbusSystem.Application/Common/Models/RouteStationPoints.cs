namespace WaterbusSystem.Application.Common.Models;

/// <summary>Toạ độ một bến.</summary>
public record StationPoint(double Latitude, double Longitude);

/// <summary>Một tuyến cùng toạ độ các bến của nó (các điểm cần hỏi thời tiết).</summary>
public record RouteStationPoints(Guid Id, string Code, string Name, IReadOnlyList<StationPoint> Points)
{
    /// <summary>Toạ độ làm tròn 2 chữ số (~1 km) và bỏ trùng: nhiều bến gần nhau chỉ cần hỏi dịch vụ ngoài một lần.</summary>
    public IReadOnlyList<(double Lat, double Lng)> RoundedDistinctPoints() =>
        Points.Select(p => (Math.Round(p.Latitude, 2), Math.Round(p.Longitude, 2))).Distinct().ToList();
}