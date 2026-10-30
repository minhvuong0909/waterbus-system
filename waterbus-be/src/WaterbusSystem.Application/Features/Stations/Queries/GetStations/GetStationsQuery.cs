using FluentValidation;
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

/// <summary>
/// Query lấy danh sách bến tàu có phân trang dành cho Admin quản trị
/// </summary>
public record GetStationsPagedQuery(
    int PageNumber = 1,
    int PageSize = 10,
    bool? IsActiveOnly = null) : IRequest<WaterbusSystem.Application.Common.Models.PaginatedList<StationDto>>;

/// <summary>M14: chặn PageNumber/PageSize âm, bằng 0 hoặc quá lớn trước khi vào Skip/Take.</summary>
public class GetStationsPagedQueryValidator : AbstractValidator<GetStationsPagedQuery>
{
    public GetStationsPagedQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize phải trong khoảng 1-100.");
    }
}

public class GetStationsPagedQueryHandler : IRequestHandler<GetStationsPagedQuery, WaterbusSystem.Application.Common.Models.PaginatedList<StationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStationsPagedQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WaterbusSystem.Application.Common.Models.PaginatedList<StationDto>> Handle(GetStationsPagedQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Stations
            .AsNoTracking()
            .Where(s => !s.IsDeleted);

        if (request.IsActiveOnly.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActiveOnly.Value);
        }

        var source = query
            .OrderBy(s => s.OrderIndex)
            .Select(s => new StationDto(
                s.Id,
                s.Code,
                s.Name,
                s.Address,
                s.Latitude,
                s.Longitude,
                s.OrderIndex,
                s.IsActive));

        return await WaterbusSystem.Application.Common.Models.PaginatedList<StationDto>.CreateAsync(
            source,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}

