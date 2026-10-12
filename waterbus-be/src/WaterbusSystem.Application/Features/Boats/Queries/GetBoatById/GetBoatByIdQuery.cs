using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Boats.Queries.GetBoats;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Boats.Queries.GetBoatById;

public record GetBoatByIdQuery(Guid Id) : IRequest<BoatDto>;

public class GetBoatByIdQueryHandler : IRequestHandler<GetBoatByIdQuery, BoatDto>
{
    private readonly IApplicationDbContext _context;

    public GetBoatByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BoatDto> Handle(GetBoatByIdQuery request, CancellationToken cancellationToken)
    {
        var boat = await _context.Boats
            .AsNoTracking()
            .Where(b => b.Id == request.Id && !b.IsDeleted)
            .Select(b => new BoatDto(
                b.Id,
                b.Code,
                b.Name,
                b.RegistrationNumber,
                b.TotalSeats,
                b.Manufacturer,
                b.YearBuilt,
                b.IsActive,
                b.CaptainUserId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Boat), request.Id);

        return boat;
    }
}
