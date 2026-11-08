namespace WaterbusSystem.Application.DTOs.Responses.SeatInventory;

public record SeatClassResponse(Guid Id, string Code, string Name, string? Description, bool IsActive, int SeatCount);

public record BoatSeatResponse(
    Guid Id, Guid BoatId, string SeatCode, Guid SeatClassId, string SeatClassCode, string SeatClassName,
    int RowNumber, int ColumnNumber, bool IsActive);

/// <summary>
/// <paramref name="State"/>: NotConfigured (chưa có ghế hoạt động), Partial (ít hơn TotalSeats), Complete (đủ TotalSeats).
/// Bất biến: ActiveSeats không bao giờ vượt TotalSeats.
/// </summary>
public record BoatSeatInventoryResponse(Guid BoatId, int TotalSeats, int ActiveSeats, string State, List<BoatSeatResponse> Seats);
