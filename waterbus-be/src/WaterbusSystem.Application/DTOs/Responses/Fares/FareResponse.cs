namespace WaterbusSystem.Application.DTOs.Responses.Fares;

/// <summary>
/// Một mức giá đang hiệu lực: BR-FARE-01 giá chỉ phân biệt theo TripType và SeatClass.
/// </summary>
public record FareResponse(string TripType, Guid SeatClassId, string SeatClassCode, string SeatClassName, decimal Price, string Currency);
