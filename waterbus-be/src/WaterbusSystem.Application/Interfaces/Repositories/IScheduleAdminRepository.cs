using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Schedules;
using WaterbusSystem.Application.DTOs.Responses.Schedules;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Interfaces.Repositories;

public interface IScheduleAdminRepository
{
    Task<PaginatedList<ScheduleListItemResponse>> GetPageAsync(GetSchedulesRequest request, CancellationToken cancellationToken = default);

    Task<ScheduleDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Schedule (có theo dõi) kèm MỌI ScheduleStop kể cả xoá mềm (unique (ScheduleId, VisitOrder) tính cả chúng), hoặc NotFound.</summary>
    Task<Schedule> GetTrackedWithStopsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Route (chỉ đọc) kèm các RouteStop chưa xoá, hoặc NotFound.</summary>
    Task<Route> GetRouteWithStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    /// <summary>Lịch đang hoạt động cùng Route/TripType/Direction/giờ khởi hành (service tự xét giao nhau ngày/hiệu lực).</summary>
    Task<List<Schedule>> ListActiveSiblingsAsync(Guid routeId, TripType tripType, string direction, TimeSpan departureTime, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Lịch đã sinh Trip (kể cả xoá mềm).</summary>
    Task<bool> HasTripsAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    void Add(Schedule schedule);

    void AddStop(ScheduleStop stop);
}
