namespace WaterbusSystem.Application.DTOs.Requests.SeatInventory;

public record SeatInput(string SeatCode, Guid SeatClassId, int RowNumber, int ColumnNumber);

/// <summary>Thêm một lô ghế vào tàu (nguyên tử: lỗi một ghế thì không ghế nào được thêm).</summary>
public record AddBoatSeatsRequest(List<SeatInput> Seats);

public record UpdateBoatSeatRequest(string SeatCode, Guid SeatClassId, int RowNumber, int ColumnNumber);
