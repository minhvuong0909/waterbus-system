namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Tính khoảng cách địa lý giữa hai tọa độ và kiểm tra vùng bán kính.
/// Application chỉ phụ thuộc interface; Infrastructure cung cấp implementation
/// NetTopologySuite với tọa độ được chiếu sang đơn vị mét.
/// Chỉ dùng cho các cặp điểm gần nhau trong geofence Boat–POI, không dùng
/// làm phép đo khoảng cách liên lục địa.
/// </summary>
public interface IGeoDistanceCalculator
{
    /// <summary>
    /// Tính khoảng cách theo mét giữa hai điểm tọa độ địa lý.
    /// </summary>
    /// <param name="lat1">Vĩ độ điểm 1 ([-90, 90]).</param>
    /// <param name="lng1">Kinh độ điểm 1 ([-180, 180]).</param>
    /// <param name="lat2">Vĩ độ điểm 2 ([-90, 90]).</param>
    /// <param name="lng2">Kinh độ điểm 2 ([-180, 180]).</param>
    /// <returns>Khoảng cách tính bằng mét.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Khi bất kỳ tọa độ nào ngoài phạm vi hợp lệ.
    /// </exception>
    double CalculateDistanceMeters(decimal lat1, decimal lng1, decimal lat2, decimal lng2);

    /// <summary>
    /// Kiểm tra một vị trí (Boat) có nằm trong bán kính kích hoạt từ một điểm (POI) hay không.
    /// </summary>
    /// <param name="boatLat">Vĩ độ tàu.</param>
    /// <param name="boatLng">Kinh độ tàu.</param>
    /// <param name="poiLat">Vĩ độ POI (nullable — POI chưa có tọa độ).</param>
    /// <param name="poiLng">Kinh độ POI (nullable — POI chưa có tọa độ).</param>
    /// <param name="triggerRadiusMeters">
    /// Bán kính kích hoạt (mét). Nullable — nếu null, phương thức trả <c>false</c>
    /// (không tự đặt giá trị mặc định).
    /// </param>
    /// <returns>
    /// <c>true</c> nếu khoảng cách ≤ <paramref name="triggerRadiusMeters"/>;
    /// <c>false</c> nếu ngoài bán kính, bán kính null hoặc tọa độ POI null.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Khi tọa độ Boat không hợp lệ hoặc khi <paramref name="triggerRadiusMeters"/> ≤ 0.
    /// </exception>
    bool IsWithinTriggerRadius(
        decimal boatLat, decimal boatLng,
        decimal? poiLat, decimal? poiLng,
        int? triggerRadiusMeters);
}
