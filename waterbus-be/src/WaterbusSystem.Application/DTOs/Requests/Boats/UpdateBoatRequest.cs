namespace WaterbusSystem.Application.DTOs.Requests.Boats;

/// <summary>Không có Id — Id lấy từ route (`{id:guid}`), tránh trùng lặp/lệch với body như trước.</summary>
public record UpdateBoatRequest(
    string Name,
    string RegistrationNumber,
    int TotalSeats,
    string Manufacturer,
    int YearBuilt,
    bool IsActive,
    Guid? CaptainUserId);
