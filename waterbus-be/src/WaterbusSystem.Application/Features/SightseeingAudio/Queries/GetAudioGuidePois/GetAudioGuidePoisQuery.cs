using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.SightseeingAudio.Queries.GetAudioGuidePois;

/// <summary>Danh sách POI thật để Admin lấy PoiId và kiểm tra AudioGuide đã publish.</summary>
public record GetAudioGuidePoisQuery(int Page = 1, int PageSize = 20)
    : IRequest<AudioGuidePoisPageDto>;

public record AudioGuidePoiDto(Guid PoiId, string Name, bool HasActiveRoutePoi,
    Guid? AudioGuideId, string? AudioAssetUri, bool IsPublished);

public record AudioGuidePoisPageDto(IReadOnlyList<AudioGuidePoiDto> Items,
    int Page, int PageSize, int TotalCount);

public sealed class GetAudioGuidePoisQueryValidator : AbstractValidator<GetAudioGuidePoisQuery>
{
    public GetAudioGuidePoisQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetAudioGuidePoisQueryHandler
    : IRequestHandler<GetAudioGuidePoisQuery, AudioGuidePoisPageDto>
{
    private readonly IApplicationDbContext _context;

    public GetAudioGuidePoisQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AudioGuidePoisPageDto> Handle(GetAudioGuidePoisQuery request,
        CancellationToken cancellationToken)
    {
        var pois = _context.PointsOfInterest.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive);
        var total = await pois.CountAsync(cancellationToken);
        var page = await pois.OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        var poiIds = page.Select(p => p.Id).ToArray();
        var activeRoutePoiIds = await _context.RoutePois.AsNoTracking()
            .Where(rp => poiIds.Contains(rp.PoiId) && !rp.IsDeleted && rp.IsActive
                && rp.TriggerRadiusM.HasValue && rp.TriggerRadiusM > 0
                && rp.Route != null && !rp.Route.IsDeleted && rp.Route.IsActive)
            .Select(rp => rp.PoiId).Distinct().ToListAsync(cancellationToken);
        var guides = await _context.AudioGuides.AsNoTracking()
            .Where(a => poiIds.Contains(a.PoiId) && !a.IsDeleted && a.LanguageCode == "vi")
            .Select(a => new { a.Id, a.PoiId, a.AudioAssetUri, a.IsPublished })
            .ToListAsync(cancellationToken);

        var routePoiSet = activeRoutePoiIds.ToHashSet();
        var guideByPoiId = guides.ToDictionary(a => a.PoiId);
        var items = page.Select(p =>
        {
            guideByPoiId.TryGetValue(p.Id, out var guide);
            return new AudioGuidePoiDto(p.Id, p.Name, routePoiSet.Contains(p.Id),
                guide?.Id, guide?.AudioAssetUri, guide?.IsPublished ?? false);
        }).ToArray();

        return new AudioGuidePoisPageDto(items, request.Page, request.PageSize, total);
    }
}
