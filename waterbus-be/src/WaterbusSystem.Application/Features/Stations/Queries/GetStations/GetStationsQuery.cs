using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Stations.Queries.GetStations;

/// <summary>
/// DTO chứa thông tin bến tàu trả về cho Web/Mobile
/// </summary>
public record StationDto(
    Guid Id,
    string Code,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int OrderIndex,
    bool IsActive);

/// <summary>
/// Query lấy danh sách toàn bộ bến tàu đang hoạt động theo thứ tự luồng sông
/// </summary>
public record GetStationsQuery : IRequest<List<StationDto>>;

public class GetStationsQueryHandler : IRequestHandler<GetStationsQuery, List<StationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StationDto>> Handle(GetStationsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Stations
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderBy(s => s.OrderIndex)
            .Select(s => new StationDto(
                s.Id,
                s.Code,
                s.Name,
                s.Address,
                s.Latitude,
                s.Longitude,
                s.OrderIndex,
                s.IsActive))
            .ToListAsync(cancellationToken);
    }
}
