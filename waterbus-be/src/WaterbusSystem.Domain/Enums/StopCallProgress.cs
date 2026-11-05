using System.Text.Json.Serialization;

namespace WaterbusSystem.Domain.Enums;

/// <summary>
/// Ghi nhận tiến độ phục vụ thực tế của Trip tại một <see cref="Entities.TripStopCall"/> cụ thể (BR-STOP-02).
/// KHÔNG làm đổi <see cref="Entities.Trip.Status"/> - tình trạng cập/rời từng bến được theo dõi riêng (BR-OPS-02).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StopCallProgress
{
    /// <summary>Tàu đã đến điểm dừng này.</summary>
    Arrived = 1,
    /// <summary>Tàu đã rời điểm dừng này (ngầm định đã đến, nếu chưa từng ghi nhận Arrived).</summary>
    Departed = 2
}
