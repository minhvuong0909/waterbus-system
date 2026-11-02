using Microsoft.EntityFrameworkCore;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.DTOs.Responses.Stations;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Stations.Services;

public sealed class StationService(IApplicationDbContext context) : IStationService
{
    public async Task<Guid> CreateAsync(CreateStationRequest request, CancellationToken cancellationToken = default)
    {
        var codeExists = await context.Stations
            .AnyAsync(s => s.Code == request.Code && !s.IsDeleted, cancellationToken);

        if (codeExists)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(request.Code), $"Mã bến '{request.Code}' đã tồn tại trong hệ thống.")
            });
        }

        var station = new Station
        {
            Code = request.Code,
            Name = request.Name,
            Address = request.Address,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            OrderIndex = request.OrderIndex,
            IsActive = true
        };

        context.Stations.Add(station);
        await context.SaveChangesAsync(cancellationToken);

        return station.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateStationRequest request, CancellationToken cancellationToken = default)
    {
        var station = await context.Stations
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Station), id);

        station.Name = request.Name;
        station.Address = request.Address;
        station.Latitude = request.Latitude;
        station.Longitude = request.Longitude;
        station.OrderIndex = request.OrderIndex;
        station.IsActive = request.IsActive;
        station.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Stations
            .AsNoTracking()
            .Where(s => s.Id == id && !s.IsDeleted)
            .Select(s => new StationResponse(s.Id, s.Code, s.Name, s.Address, s.Latitude, s.Longitude, s.OrderIndex, s.IsActive))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Station), id);

    public Task<List<StationResponse>> GetActiveStationsAsync(CancellationToken cancellationToken = default) =>
        context.Stations
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderBy(s => s.OrderIndex)
            .Select(s => new StationResponse(s.Id, s.Code, s.Name, s.Address, s.Latitude, s.Longitude, s.OrderIndex, s.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<PaginatedList<StationResponse>> GetPageAsync(GetStationsPagedRequest request, CancellationToken cancellationToken = default)
    {
        var query = context.Stations
            .AsNoTracking()
            .Where(s => !s.IsDeleted);

        if (request.IsActiveOnly.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActiveOnly.Value);
        }

        var source = query
            .OrderBy(s => s.OrderIndex)
            .Select(s => new StationResponse(s.Id, s.Code, s.Name, s.Address, s.Latitude, s.Longitude, s.OrderIndex, s.IsActive));

        return await PaginatedList<StationResponse>.CreateAsync(source, request.PageNumber, request.PageSize, cancellationToken);
    }
}
