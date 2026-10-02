using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Stations.Queries.GetStations;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Stations.Queries.GetStationById;

public record GetStationByIdQuery(Guid Id) : IRequest<StationDto>;

public class GetStationByIdQueryHandler : IRequestHandler<GetStationByIdQuery, StationDto>
{
    private readonly IApplicationDbContext _context;

    public GetStationByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StationDto> Handle(GetStationByIdQuery request, CancellationToken cancellationToken)
    {
        var station = await _context.Stations
            .AsNoTracking()
            .Where(s => s.Id == request.Id && !s.IsDeleted)
            .Select(s => new StationDto(
                s.Id,
                s.Code,
                s.Name,
                s.Address,
                s.Latitude,
                s.Longitude,
                s.OrderIndex,
                s.IsActive))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Station), request.Id);

        return station;
    }
}
