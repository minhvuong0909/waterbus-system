namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Phân loại vị trí khoang ghế trên tàu
/// </summary>
public enum SeatCategory
{
    /// <summary>
    /// Khoang trước (VIP): Tầm nhìn bao quát hướng mũi tàu, êm ái
    /// </summary>
    FrontCabin = 1,

    /// <summary>
    /// Khoang tiêu chuẩn: Nằm bên trong khoang có máy lạnh, lối đi giữa
    /// </summary>
    Standard = 2,

    /// <summary>
    /// Boong ngoài trời: Phía đuôi tàu, thoáng mát ngắm cảnh sông và cầu Ba Son / Landmark 81
    /// </summary>
    Outdoor = 3
}
