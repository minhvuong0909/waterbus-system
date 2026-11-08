using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Truy vấn chỉ đọc cho lịch sử Order/Ticket của Passenger và TripAccess (TripAccess không lộ dữ liệu thanh toán).</summary>
public interface IAccountAccessRepository
{
    Task<PaginatedList<MyOrderSummaryDto>> GetOrdersPageAsync(Guid accountId, GetMyOrdersRequest request, CancellationToken ct);
    Task<TicketDetailDto?> FindTicketAsync(Guid orderId, Guid ticketId, CancellationToken ct);
    Task<TicketDetailDto?> FindTicketForAccountAsync(Guid accountId, Guid ticketId, CancellationToken ct);
    Task<TripAccessDto?> FindTripAccessAsync(Guid bookingId, CancellationToken ct);
    Task<BookingStatus?> GetBookingStatusAsync(Guid orderId, Guid bookingId, CancellationToken ct);
}
