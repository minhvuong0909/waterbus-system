using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Schedules;
using WaterbusSystem.Application.DTOs.Responses.Schedules;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface IScheduleAdminService
{
    Task<PaginatedList<ScheduleListItemResponse>> GetPageAsync(GetSchedulesRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateScheduleRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleDetailResponse> ReplaceStopsAsync(Guid id, ReplaceScheduleStopsRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);
}
