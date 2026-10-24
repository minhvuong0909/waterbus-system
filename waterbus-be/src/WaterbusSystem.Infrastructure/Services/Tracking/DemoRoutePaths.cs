namespace WaterbusSystem.Infrastructure.Services.Tracking;

/// <summary>
/// Đường đi thực tế dọc sông của các tuyến demo, dùng cho bộ giả lập GPS.
///
/// Tuyến Route chỉ biết các bến nên nối các bến bằng đường thẳng, cắt qua đất ở các khúc cua của sông
/// Sài Gòn (đặc biệt quanh bán đảo Thanh Đa). Dữ liệu này chỉ phục vụ DEMO: chưa có mô hình dữ liệu cho
/// đường đi của tuyến, khi có thì thay thế và xoá file này. FE có bản sao ở waterbus-fe/src/constant/riverPaths.ts.
///
/// Nguồn: OpenStreetMap (c) OpenStreetMap contributors, ODbL, các đoạn "Saigon Waterbus" của tuyến số 1
/// (relation 13684620), chiều Bạch Đằng -> Linh Đông, đã làm thưa (sai số 4 m). Đi qua đúng 5 bến theo thứ tự.
/// </summary>
public static class DemoRoutePaths
{
    /// <summary>Đường đi theo mã tuyến; null nếu tuyến không có đường riêng.</summary>
    public static IReadOnlyList<RoutePathInterpolator.GeoPoint>? For(string? routeCode) =>
        routeCode is not null && Paths.TryGetValue(routeCode, out var path) ? path : null;

    private static readonly RoutePathInterpolator.GeoPoint[] Rt01 =
    [
        new(10.77523, 106.707282),
        new(10.775331, 106.707633),
        new(10.775549, 106.707874),
        new(10.778085, 106.708288),
        new(10.779811, 106.70879),
        new(10.781483, 106.709338),
        new(10.782137, 106.709687),
        new(10.782384, 106.709886),
        new(10.782878, 106.710359),
        new(10.783764, 106.711394),
        new(10.785719, 106.714436),
        new(10.786083, 106.715345),
        new(10.786308, 106.716296),
        new(10.786559, 106.717749),
        new(10.78689, 106.719163),
        new(10.787251, 106.720386),
        new(10.788277, 106.722443),
        new(10.788803, 106.723272),
        new(10.789074, 106.723549),
        new(10.790361, 106.724456),
        new(10.791838, 106.725352),
        new(10.792976, 106.725952),
        new(10.794634, 106.726413),
        new(10.796855, 106.726584),
        new(10.796931, 106.726636),
        new(10.79703, 106.726878),
        new(10.79736, 106.728388),
        new(10.797459, 106.728033),
        new(10.797728, 106.727846),
        new(10.80064, 106.727491),
        new(10.801306, 106.727323),
        new(10.801733, 106.727446),
        new(10.80187, 106.727569),
        new(10.80177, 106.727291),
        new(10.801839, 106.727049),
        new(10.805533, 106.724247),
        new(10.806927, 106.72355),
        new(10.808046, 106.723108),
        new(10.809606, 106.722641),
        new(10.809833, 106.722385),
        new(10.810407, 106.721034),
        new(10.810695, 106.720567),
        new(10.812444, 106.719297),
        new(10.81478, 106.717441),
        new(10.818212, 106.714888),
        new(10.81898, 106.714588),
        new(10.819312, 106.71459),
        new(10.819679, 106.714739),
        new(10.819826, 106.714919),
        new(10.819869, 106.715169),
        new(10.819606, 106.716637),
        new(10.81979, 106.716756),
        new(10.819899, 106.717026),
        new(10.819924, 106.71977),
        new(10.820107, 106.721221),
        new(10.820456, 106.722983),
        new(10.821019, 106.724727),
        new(10.821502, 106.725947),
        new(10.82234, 106.727728),
        new(10.82297, 106.728923),
        new(10.824138, 106.730573),
        new(10.825208, 106.731856),
        new(10.825455, 106.732066),
        new(10.82587, 106.732166),
        new(10.826073, 106.732124),
        new(10.827049, 106.731673),
        new(10.826765, 106.732431),
        new(10.826714, 106.732738),
        new(10.826754, 106.732969),
        new(10.826951, 106.733291),
        new(10.830028, 106.735303),
        new(10.832559, 106.736639),
        new(10.833374, 106.737281),
        new(10.834305, 106.738369),
        new(10.834809, 106.739488),
        new(10.834967, 106.740347),
        new(10.835005, 106.740953),
        new(10.834892, 106.741727),
        new(10.834017, 106.744902),
        new(10.833986, 106.745379),
        new(10.83412, 106.745742),
        new(10.834235, 106.745898),
        new(10.834504, 106.74614),
        new(10.83496, 106.746407)
    ];

    private static readonly Dictionary<string, IReadOnlyList<RoutePathInterpolator.GeoPoint>> Paths = new(StringComparer.OrdinalIgnoreCase)
    {
        // Bạch Đằng -> Bình An -> Thanh Đa -> Hiệp Bình Chánh -> Linh Đông
        ["RT01"] = Rt01,
        // Linh Đông -> Hiệp Bình Chánh -> Thanh Đa -> Bình An -> Bạch Đằng
        ["RT02"] = Rt01.Reverse().ToArray()
    };
}
