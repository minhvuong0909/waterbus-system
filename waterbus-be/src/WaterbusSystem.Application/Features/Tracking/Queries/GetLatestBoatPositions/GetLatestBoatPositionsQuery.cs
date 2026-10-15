using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Tracking.Queries.GetLatestBoatPositions;

public record BoatLatestPositionDto(
    Guid BoatId,
    string BoatCode,
    string BoatName,
    Guid? TripId,
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKnots,
    DateTimeOffset ObservedAt);

/// <summary>
/// Snapshot vị trí mới nhất của các tàu đang hoạt động (radar Admin gọi khi mở trang / sau khi reconnect SignalR).
/// Đọc từ cache Redis, thiếu thì lấy bản ghi BoatPositionEvent mới nhất trong SQL. Tàu chưa có vị trí sẽ không xuất hiện.
/// </summary>
public record GetLatestBoatPositionsQuery : IRequest<List<BoatLatestPositionDto>>;

public class GetLatestBoatPositionsQueryHandler : IRequestHandler<GetLatestBoatPositionsQuery, List<BoatLatestPositionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IBoatPositionCache _cache;

    public GetLatestBoatPositionsQueryHandler(IApplicationDbContext context, IBoatPositionCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<BoatLatestPositionDto>> Handle(GetLatestBoatPositionsQuery request, CancellationToken cancellationToken)
    {
        var boats = await _context.Boats.AsNoTracking()
            .Where(b => !b.IsDeleted && b.IsActive)
            .OrderBy(b => b.Code)
            .Select(b => new { b.Id, b.Code, b.Name })
            .ToListAsync(cancellationToken);

        var cached = (await _cache.GetAllAsync(cancellationToken)).ToDictionary(m => m.BoatId);

        var result = new List<BoatLatestPositionDto>();
        foreach (var boat in boats)
        {
            if (cached.TryGetValue(boat.Id, out var position))
            {
                result.Add(new BoatLatestPositionDto(boat.Id, boat.Code, boat.Name, position.TripId,
                    position.Lat, position.Lng, position.SpeedKnots, position.ObservedAt));
                continue;
            }

            var persisted = await _context.BoatPositionEvents.AsNoTracking()
                .Where(e => e.BoatId == boat.Id)
                .OrderByDescending(e => e.ObservedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (persisted is not null)
            {
                result.Add(new BoatLatestPositionDto(boat.Id, boat.Code, boat.Name, persisted.TripId,
                    persisted.Latitude, persisted.Longitude, persisted.SpeedKnots, persisted.ObservedAt));
            }
        }

        return result;
    }
}
