using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Thực thể Tàu thủy chở khách (Saigon Waterbus)
/// </summary>
public class Boat : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public int YearBuilt { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Danh sách các ghế cố định trên tàu
    /// </summary>
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
}
