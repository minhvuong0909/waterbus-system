using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.DTOs.Responses.Stations;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class StationRepository(ApplicationDbContext context) : IStationRepository
{
    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        context.Stations.AnyAsync(s => s.Code == code && !s.IsDeleted, cancellationToken);

    public async Task<Station> GetRequiredByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Stations.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(Station), id);

    public async Task<StationResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Stations.AsNoTracking()
            .Where(s => s.Id == id && !s.IsDeleted)
            .Select(s => new StationResponse(s.Id, s.Code, s.Name, s.Address, s.Latitude, s.Longitude, s.OrderIndex, s.IsActive))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Station), id);

    public Task<List<StationResponse>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        context.Stations.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderBy(s => s.OrderIndex)
            .Select(s => new StationResponse(s.Id, s.Code, s.Name, s.Address, s.Latitude, s.Longitude, s.OrderIndex, s.IsActive))
            .ToListAsync(cancellationToken);

    public Task<PaginatedList<StationResponse>> GetPageAsync(GetStationsPagedRequest request, CancellationToken cancellationToken = default)
    {
        var query = context.Stations.AsNoTracking().Where(s => !s.IsDeleted);

        if (request.IsActiveOnly.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActiveOnly.Value);
        }

        var source = query
            .OrderBy(s => s.OrderIndex)
            .Select(s => new StationResponse(s.Id, s.Code, s.Name, s.Address, s.Latitude, s.Longitude, s.OrderIndex, s.IsActive));

        return source.ToPaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);
    }

    public void Add(Station station) => context.Stations.Add(station);
}
