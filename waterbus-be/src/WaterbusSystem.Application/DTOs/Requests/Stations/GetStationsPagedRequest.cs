namespace WaterbusSystem.Application.DTOs.Requests.Stations;

/// <summary>Danh sách bến tàu có phân trang dành cho Admin quản trị.</summary>
public record GetStationsPagedRequest(
    int PageNumber = 1,
    int PageSize = 10,
    bool? IsActiveOnly = null);
