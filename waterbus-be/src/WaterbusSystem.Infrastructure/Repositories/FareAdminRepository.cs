using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Fares;
using WaterbusSystem.Application.DTOs.Responses.Fares;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class FareAdminRepository(ApplicationDbContext context) : IFareAdminRepository
{
    public Task<PaginatedList<FareRuleAdminResponse>> GetPageAsync(GetFareRulesRequest request, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var query = context.FareRules.AsNoTracking().Where(f => !f.IsDeleted);
        if (request.TripType != null) query = query.Where(f => f.TripType == request.TripType);
        if (request.SeatClassId.HasValue) query = query.Where(f => f.SeatClassId == request.SeatClassId.Value);
        if (request.IsActive.HasValue) query = query.Where(f => f.IsActive == request.IsActive.Value);

        return query.OrderBy(f => f.TripType).ThenBy(f => f.SeatClass!.Code).ThenByDescending(f => f.EffectiveFrom)
            .Select(f => new FareRuleAdminResponse(f.Id, f.TripType, f.SeatClassId, f.SeatClass!.Code, f.SeatClass.Name, f.Price, f.Currency,
                f.EffectiveFrom, f.EffectiveTo, f.IsActive,
                !f.IsActive ? "Inactive" : f.EffectiveFrom > now ? "Upcoming" : f.EffectiveTo != null && f.EffectiveTo <= now ? "Expired" : "Active"))
            .ToPaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);
    }

    public async Task<FareRuleAdminResponse> GetDetailAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await context.FareRules.AsNoTracking().Where(f => f.Id == id && !f.IsDeleted)
            .Select(f => new FareRuleAdminResponse(f.Id, f.TripType, f.SeatClassId, f.SeatClass!.Code, f.SeatClass.Name, f.Price, f.Currency,
                f.EffectiveFrom, f.EffectiveTo, f.IsActive,
                !f.IsActive ? "Inactive" : f.EffectiveFrom > now ? "Upcoming" : f.EffectiveTo != null && f.EffectiveTo <= now ? "Expired" : "Active"))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(FareRule), id);

    public async Task<FareRule> GetTrackedAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.FareRules.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(nameof(FareRule), id);

    public Task<SeatClass?> FindSeatClassAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SeatClasses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);

    public Task<List<FareRule>> ListActiveSiblingsAsync(string tripType, Guid seatClassId, Guid? excludeId, CancellationToken cancellationToken = default) =>
        context.FareRules.AsNoTracking()
            .Where(f => f.TripType == tripType && f.SeatClassId == seatClassId && f.IsActive && f.Id != excludeId)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsWithSameStartAsync(string tripType, Guid seatClassId, DateTimeOffset effectiveFrom, Guid? excludeId, CancellationToken cancellationToken = default) =>
        context.FareRules.IgnoreQueryFilters().AnyAsync(
            f => f.TripType == tripType && f.SeatClassId == seatClassId && f.EffectiveFrom == effectiveFrom && f.Id != excludeId, cancellationToken);

    public void Add(FareRule fareRule) => context.FareRules.Add(fareRule);
}
