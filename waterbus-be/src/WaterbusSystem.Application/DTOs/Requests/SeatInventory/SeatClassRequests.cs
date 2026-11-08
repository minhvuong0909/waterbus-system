namespace WaterbusSystem.Application.DTOs.Requests.SeatInventory;

public record CreateSeatClassRequest(string Code, string Name, string? Description);

/// <summary>Mã hạng ghế không đổi được (FareRule/lịch sử tham chiếu theo hạng).</summary>
public record UpdateSeatClassRequest(string Name, string? Description);

public record GetSeatClassesRequest(bool IncludeInactive = false);
