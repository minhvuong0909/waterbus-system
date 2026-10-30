using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Features.Boats.Queries.GetBoats;

public record BoatDto(
    Guid Id,
    string Code,
    string Name,
    string RegistrationNumber,
    int TotalSeats,
    string Manufacturer,
    int YearBuilt,
    bool IsActive,
    Guid? CaptainUserId);

public record GetBoatsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    bool? IsActiveOnly = null) : IRequest<PaginatedList<BoatDto>>;

/// <summary>M14: chặn PageNumber/PageSize âm, bằng 0 hoặc quá lớn trước khi vào Skip/Take.</summary>
public class GetBoatsQueryValidator : AbstractValidator<GetBoatsQuery>
{
    public GetBoatsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize phải trong khoảng 1-100.");
    }
}

public class GetBoatsQueryHandler : IRequestHandler<GetBoatsQuery, PaginatedList<BoatDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBoatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<BoatDto>> Handle(GetBoatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Boats
            .AsNoTracking()
            .Where(b => !b.IsDeleted);

        if (request.IsActiveOnly.HasValue)
        {
            query = query.Where(b => b.IsActive == request.IsActiveOnly.Value);
        }

        var source = query
            .OrderBy(b => b.Code)
            .Select(b => new BoatDto(
                b.Id,
                b.Code,
                b.Name,
                b.RegistrationNumber,
                b.TotalSeats,
                b.Manufacturer,
                b.YearBuilt,
                b.IsActive,
                b.CaptainUserId));

        return await PaginatedList<BoatDto>.CreateAsync(
            source,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
