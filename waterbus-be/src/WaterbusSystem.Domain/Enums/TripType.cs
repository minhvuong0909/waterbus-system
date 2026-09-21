namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Phân loại chuyến buýt đường sông
/// </summary>
public enum TripType
{
    /// <summary>
    /// Chuyến thường (Commuter): Phục vụ đi lại hàng ngày, giá tiêu chuẩn, tắt audio guide du lịch để giữ yên tĩnh
    /// </summary>
    Commuter = 1,

    /// <summary>
    /// Chuyến du lịch (Sightseeing): Có hệ thống AI Audio Guide thuyết minh di tích ven sông, giá vé du lịch
    /// </summary>
    Sightseeing = 2
}
