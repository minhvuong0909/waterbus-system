namespace WaterbusSystem.Application.DTOs.Requests.Accounts;

public record GetMyOrdersRequest(int PageNumber = 1, int PageSize = 10);
