using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class ScannerDeviceAdminRepository(ApplicationDbContext context) : IScannerDeviceAdminRepository
{
    public Task<PaginatedList<ScannerDeviceResponse>> GetPageAsync(GetScannerDevicesRequest request, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var query = context.ScannerDevices.AsNoTracking().Where(d => !d.IsDeleted);
        if (request.IsActive.HasValue) query = query.Where(d => d.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(d => d.DeviceCode.ToLower().Contains(term) || (d.Label != null && d.Label.ToLower().Contains(term)));
        }

        return query.OrderBy(d => d.DeviceCode)
            .Select(d => new ScannerDeviceResponse(d.Id, d.DeviceCode, d.Label, d.IsActive, d.LastPreloadedAt,
                context.ScannerAssignments.Count(a => a.DeviceId == d.Id && (a.ActiveUntil == null || (a.ActiveUntil > a.ActiveFrom && a.ActiveUntil > now))),
                context.CheckInEvents.Count(e => e.ScannerDeviceId == d.Id)))
            .ToPaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);
    }

    public async Task<ScannerDeviceResponse> GetDetailAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await context.ScannerDevices.AsNoTracking().Where(d => d.Id == id && !d.IsDeleted)
            .Select(d => new ScannerDeviceResponse(d.Id, d.DeviceCode, d.Label, d.IsActive, d.LastPreloadedAt,
                context.ScannerAssignments.Count(a => a.DeviceId == d.Id && (a.ActiveUntil == null || (a.ActiveUntil > a.ActiveFrom && a.ActiveUntil > now))),
                context.CheckInEvents.Count(e => e.ScannerDeviceId == d.Id)))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(ScannerDevice), id);

    public async Task<ScannerDevice> GetTrackedAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.ScannerDevices.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(ScannerDevice), id);

    public Task<bool> ExistsByCodeAsync(string deviceCode, CancellationToken cancellationToken = default) =>
        context.ScannerDevices.IgnoreQueryFilters().AnyAsync(d => d.DeviceCode == deviceCode, cancellationToken);

    public Task<int> CountActiveAssignmentsAsync(Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.ScannerAssignments.CountAsync(a => a.DeviceId == deviceId && (a.ActiveUntil == null || (a.ActiveUntil > a.ActiveFrom && a.ActiveUntil > now)), cancellationToken);

    public void Add(ScannerDevice device) => context.ScannerDevices.Add(device);
}
