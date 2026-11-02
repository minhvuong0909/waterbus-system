using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Stations;
using WaterbusSystem.Application.DTOs.Responses.Stations;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Features.Stations.Services;

public sealed class StationService(IStationRepository stations, IUnitOfWork unitOfWork) : IStationService
{
    public async Task<Guid> CreateAsync(CreateStationRequest request, CancellationToken cancellationToken = default)
    {
        if (await stations.ExistsByCodeAsync(request.Code, cancellationToken))
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

        stations.Add(station);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return station.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateStationRequest request, CancellationToken cancellationToken = default)
    {
        var station = await stations.GetRequiredByIdAsync(id, cancellationToken);

        station.Name = request.Name;
        station.Address = request.Address;
        station.Latitude = request.Latitude;
        station.Longitude = request.Longitude;
        station.OrderIndex = request.OrderIndex;
        station.IsActive = request.IsActive;
        station.UpdatedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<StationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        stations.GetDetailAsync(id, cancellationToken);

    public Task<List<StationResponse>> GetActiveStationsAsync(CancellationToken cancellationToken = default) =>
        stations.ListActiveAsync(cancellationToken);

    public Task<PaginatedList<StationResponse>> GetPageAsync(GetStationsPagedRequest request, CancellationToken cancellationToken = default) =>
        stations.GetPageAsync(request, cancellationToken);
}
