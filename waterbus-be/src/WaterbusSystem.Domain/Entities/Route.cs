using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Tuyến đường sông kết nối giữa các bến
/// </summary>
public class Route : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Guid DepartureStationId { get; set; }
    public Station? DepartureStation { get; set; }

    public Guid ArrivalStationId { get; set; }
    public Station? ArrivalStation { get; set; }

    /// <summary>
    /// Thời gian di chuyển dự kiến (phút)
    /// </summary>
    public int EstimatedDurationMinutes { get; set; }

    /// <summary>
    /// Chiều dài quãng đường đường sông (km)
    /// </summary>
    public decimal DistanceKm { get; set; }

    public bool IsActive { get; set; } = true;
}
