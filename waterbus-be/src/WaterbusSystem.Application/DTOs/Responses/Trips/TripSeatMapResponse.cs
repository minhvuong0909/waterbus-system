namespace WaterbusSystem.Application.DTOs.Responses.Trips;

public record TripSeatResponse(Guid SeatId, string SeatCode, int RowNumber, int ColumnNumber,
    Guid SeatClassId, string? SeatClassName, decimal? Price, string Currency, string Status,
    string? UnavailableReason, DateTimeOffset? HoldExpiresAt);

public record TripSeatMapResponse(Guid TripId, Guid BoatId, Guid BoardingCallId, Guid DisembarkingCallId,
    int BoardingVisitOrder, int DisembarkingVisitOrder, bool CanBook, string SalesState,
    int AvailableSeatsCount, DateTimeOffset AsOf, IReadOnlyList<TripSeatResponse> Seats);
