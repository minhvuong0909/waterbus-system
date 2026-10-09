using FluentAssertions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Infrastructure.Services;
using Xunit;

namespace WaterbusSystem.UnitTests.Services;

/// <summary>
/// Unit tests cho HaversineDistanceCalculator — geofence core Boat–POI.
///
/// Công thức: Haversine trên mặt cầu, R = 6 371 000 m.
/// Đơn vị: mét.
/// Tolerance được ghi rõ trong từng test khoảng cách.
///
/// Tọa độ mẫu quanh TP.HCM:
///   Bến Nhà Rồng  (10.768462, 106.705513)
///   Bến Bạch Đằng (10.774968, 106.704710)  ≈ 729 m đường chim bay
/// </summary>
public class HaversineDistanceCalculatorTests
{
    private readonly IGeoDistanceCalculator _calculator = new HaversineDistanceCalculator();

    // ── Tọa độ mẫu ──────────────────────────────────────────────────────────

    // Bến Nhà Rồng, TP.HCM
    private const decimal NhaRongLat = 10.768462m;
    private const decimal NhaRongLng = 106.705513m;

    // Bến Bạch Đằng, TP.HCM
    private const decimal BachDangLat = 10.774968m;
    private const decimal BachDangLng = 106.704710m;

    // ── CalculateDistanceMeters ───────────────────────────────────────────

    [Fact]
    public void CalculateDistanceMeters_SamePoint_ReturnsZero()
    {
        var distance = _calculator.CalculateDistanceMeters(
            NhaRongLat, NhaRongLng, NhaRongLat, NhaRongLng);

        distance.Should().Be(0.0);
    }

    [Fact]
    public void CalculateDistanceMeters_KnownDistance_NhaRongToBachDang()
    {
        // Khoảng cách mặt cầu giữa hai tọa độ ≈ 729 m.
        var distance = _calculator.CalculateDistanceMeters(
            NhaRongLat, NhaRongLng, BachDangLat, BachDangLng);

        distance.Should().BeApproximately(729, 1,
            "hai tọa độ mẫu cách nhau khoảng 729 m đường chim bay");
    }

    [Fact]
    public void CalculateDistanceMeters_IsSymmetric()
    {
        var d1 = _calculator.CalculateDistanceMeters(
            NhaRongLat, NhaRongLng, BachDangLat, BachDangLng);
        var d2 = _calculator.CalculateDistanceMeters(
            BachDangLat, BachDangLng, NhaRongLat, NhaRongLng);

        d1.Should().BeApproximately(d2, 0.001, "khoảng cách phải đối xứng");
    }

    [Fact]
    public void CalculateDistanceMeters_Equator_OneDegree_Approximately111km()
    {
        // 1° kinh độ tại xích đạo ≈ 111 195 m
        var distance = _calculator.CalculateDistanceMeters(0m, 0m, 0m, 1m);

        distance.Should().BeApproximately(111_195, 50,
            "1° kinh độ tại xích đạo ≈ 111.2 km");
    }

    [Fact]
    public void CalculateDistanceMeters_NearAntipodes_ReturnsFiniteDistance()
    {
        // Phép tính a chưa clamp có thể lớn hơn 1 do sai số double ở cặp điểm này.
        var distance = _calculator.CalculateDistanceMeters(
            58.531077205123m, -166.932327707678m,
            -58.531077242074m, 13.067672318334m);

        double.IsFinite(distance).Should().BeTrue();
        distance.Should().BeApproximately(Math.PI * 6_371_000, 1);
    }

    // ── Invalid coordinates ──────────────────────────────────────────────

    [Theory]
    [InlineData(91, 0, 0, 0)]     // lat1 quá cao
    [InlineData(-91, 0, 0, 0)]    // lat1 quá thấp
    [InlineData(0, 181, 0, 0)]    // lng1 quá lớn
    [InlineData(0, -181, 0, 0)]   // lng1 quá nhỏ
    [InlineData(0, 0, 91, 0)]     // lat2 quá cao
    [InlineData(0, 0, 0, 181)]    // lng2 quá lớn
    public void CalculateDistanceMeters_InvalidCoordinate_ThrowsArgumentOutOfRange(
        double lat1, double lng1, double lat2, double lng2)
    {
        var act = () => _calculator.CalculateDistanceMeters(
            (decimal)lat1, (decimal)lng1, (decimal)lat2, (decimal)lng2);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateDistanceMeters_BoundaryCoordinates_DoesNotThrow()
    {
        // Các giá trị biên: (-90, -180) đến (90, 180)
        var act = () => _calculator.CalculateDistanceMeters(-90m, -180m, 90m, 180m);

        act.Should().NotThrow();
    }

    // ── IsWithinTriggerRadius ─────────────────────────────────────────────

    [Fact]
    public void IsWithinTriggerRadius_InsideRadius_ReturnsTrue()
    {
        // Bến Nhà Rồng ↔ Bến Bạch Đằng ≈ 726 m, radius 800 m → trong
        var result = _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, BachDangLat, BachDangLng, 800);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinTriggerRadius_OutsideRadius_ReturnsFalse()
    {
        // Khoảng cách ≈ 726 m, radius 500 m → ngoài
        var result = _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, BachDangLat, BachDangLng, 500);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinTriggerRadius_SamePointInsidePositiveRadius_ReturnsTrue()
    {
        var result = _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, NhaRongLat, NhaRongLng, 1);

        result.Should().BeTrue("khoảng cách 0 m ≤ radius 1 m");
    }

    [Fact]
    public void IsWithinTriggerRadius_JustInside100MeterBoundary_ReturnsTrue()
    {
        // Trên xích đạo: chênh kinh độ này tương đương xấp xỉ 99,99 m.
        var result = _calculator.IsWithinTriggerRadius(
            0m, 0m, 0m, 0.00089923m, 100);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinTriggerRadius_JustOutside100MeterBoundary_ReturnsFalse()
    {
        // Trên xích đạo: chênh kinh độ này tương đương xấp xỉ 100,01 m.
        var result = _calculator.IsWithinTriggerRadius(
            0m, 0m, 0m, 0.00089941m, 100);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinTriggerRadius_NullRadius_ReturnsFalse()
    {
        // TriggerRadiusM = null → không tự đặt default → false
        var result = _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, BachDangLat, BachDangLng, null);

        result.Should().BeFalse("null radius phải trả false, không tự đặt mặc định");
    }

    [Fact]
    public void IsWithinTriggerRadius_NullPoiLatitude_ReturnsFalse()
    {
        var result = _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, null, BachDangLng, 500);

        result.Should().BeFalse("POI thiếu vĩ độ phải trả false");
    }

    [Fact]
    public void IsWithinTriggerRadius_NullPoiLongitude_ReturnsFalse()
    {
        var result = _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, BachDangLat, null, 500);

        result.Should().BeFalse("POI thiếu kinh độ phải trả false");
    }

    [Fact]
    public void IsWithinTriggerRadius_NullPoiBothCoords_ReturnsFalse()
    {
        var result = _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, null, null, 500);

        result.Should().BeFalse("POI không có tọa độ phải trả false");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void IsWithinTriggerRadius_NonPositiveRadius_ThrowsArgumentOutOfRange(int radius)
    {
        var act = () => _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, BachDangLat, BachDangLng, radius);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("triggerRadiusMeters");
    }

    [Fact]
    public void IsWithinTriggerRadius_InvalidBoatCoordinate_ThrowsArgumentOutOfRange()
    {
        var act = () => _calculator.IsWithinTriggerRadius(
            91m, 0m, BachDangLat, BachDangLng, 500);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsWithinTriggerRadius_InvalidBoatCoordinateAndNullPoi_StillThrows()
    {
        var act = () => _calculator.IsWithinTriggerRadius(
            91m, 0m, null, null, 500);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("boatLat");
    }

    [Fact]
    public void IsWithinTriggerRadius_InvalidBoatCoordinateAndNullRadius_StillThrows()
    {
        var act = () => _calculator.IsWithinTriggerRadius(
            91m, 0m, BachDangLat, BachDangLng, null);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("boatLat");
    }

    [Fact]
    public void IsWithinTriggerRadius_NonPositiveRadiusAndNullPoi_StillThrows()
    {
        var act = () => _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, null, null, 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("triggerRadiusMeters");
    }

    [Fact]
    public void IsWithinTriggerRadius_InvalidPoiCoordinate_ThrowsArgumentOutOfRange()
    {
        var act = () => _calculator.IsWithinTriggerRadius(
            NhaRongLat, NhaRongLng, 91m, 0m, 500);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
