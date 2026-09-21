using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Thực thể Bến tàu (Ga đón/trả khách dọc sông Sài Gòn)
/// </summary>
public class Station : BaseEntity
{
    /// <summary>
    /// Mã bến tàu (Ví dụ: ST01, ST02) - Đánh Unique Index
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Tên bến tàu (Ví dụ: Bến Bạch Đằng, Bến Bình An)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ thực tế
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Tọa độ Vĩ độ (Latitude) phục vụ tính toán Geofence và hiển thị bản đồ Radar
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Tọa độ Kinh độ (Longitude)
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Thứ tự bến dọc theo luồng sông từ đầu nguồn đến cuối nguồn
    /// </summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// Trạng thái hoạt động (bật/tắt khi bảo trì hoặc ảnh hưởng bão)
    /// </summary>
    public bool IsActive { get; set; } = true;
}
