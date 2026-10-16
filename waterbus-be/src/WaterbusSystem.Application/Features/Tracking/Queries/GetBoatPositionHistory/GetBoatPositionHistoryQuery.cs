using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Tracking.Queries.GetBoatPositionHistory;

public record BoatPositionHistoryDto(
    Guid Id,
    Guid? TripId,
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKnots,
    DateTimeOffset ObservedAt,
    string Source);

/// <summary>
/// Lịch sử vị trí đã lưu (thưa) của một tàu trong khoảng thời gian, sắp theo thời gian tăng dần, có phân trang.
/// </summary>
public record GetBoatPositionHistoryQuery(
    Guid BoatId,
    DateTimeOffset From,
    DateTimeOffset To,
    int PageNumber = 1,
    int PageSize = 100) : IRequest<PaginatedList<BoatPositionHistoryDto>>;

public class GetBoatPositionHistoryQueryValidator : AbstractValidator<GetBoatPositionHistoryQuery>
{
    public GetBoatPositionHistoryQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From).WithMessage("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
        RuleFor(x => x)
            .Must(x => x.To - x.From <= TimeSpan.FromDays(7))
            .WithName("To")
            .WithMessage("Chỉ được truy vấn tối đa 7 ngày mỗi lần.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("Số trang phải từ 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500).WithMessage("Kích thước trang phải trong [1, 500].");
    }
}

public class GetBoatPositionHistoryQueryHandler : IRequestHandler<GetBoatPositionHistoryQuery, PaginatedList<BoatPositionHistoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBoatPositionHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<BoatPositionHistoryDto>> Handle(GetBoatPositionHistoryQuery request, CancellationToken cancellationToken)
    {
        var boatExists = await _context.Boats.AnyAsync(b => b.Id == request.BoatId && !b.IsDeleted, cancellationToken);
        if (!boatExists)
        {
            throw new NotFoundException(nameof(Boat), request.BoatId);
        }

        var query = _context.BoatPositionEvents.AsNoTracking()
            .Where(e => e.BoatId == request.BoatId && e.ObservedAt >= request.From && e.ObservedAt <= request.To)
            .OrderBy(e => e.ObservedAt)
            .Select(e => new BoatPositionHistoryDto(
                e.Id, e.TripId, e.Latitude, e.Longitude, e.SpeedKnots, e.ObservedAt, e.Source));

        return await PaginatedList<BoatPositionHistoryDto>.CreateAsync(query, request.PageNumber, request.PageSize, cancellationToken);
    }
}
