namespace WaterbusSystem.Application.DTOs.Requests.Boats;

public record GetBoatsRequest(
    int PageNumber = 1,
    int PageSize = 10,
    bool? IsActiveOnly = null);
