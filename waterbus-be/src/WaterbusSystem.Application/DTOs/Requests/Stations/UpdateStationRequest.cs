namespace WaterbusSystem.Application.DTOs.Requests.Stations;

/// <summary>Không có Id — Id lấy từ route (`{id:guid}`), tránh trùng lặp/lệch với body như trước.</summary>
public record UpdateStationRequest(
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int OrderIndex,
    bool IsActive);
