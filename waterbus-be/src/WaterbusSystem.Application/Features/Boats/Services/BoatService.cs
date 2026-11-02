using Microsoft.EntityFrameworkCore;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.DTOs.Responses.Boats;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Boats.Services;

public sealed class BoatService(IApplicationDbContext context) : IBoatService
{
    public async Task<Guid> CreateAsync(CreateBoatRequest request, CancellationToken cancellationToken = default)
    {
        var codeExists = await context.Boats
            .AnyAsync(b => b.Code == request.Code && !b.IsDeleted, cancellationToken);

        if (codeExists)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(request.Code), $"Mã tàu '{request.Code}' đã tồn tại trong hệ thống.")
            });
        }

        var boat = new Boat
        {
            Code = request.Code,
            Name = request.Name,
            RegistrationNumber = request.RegistrationNumber,
            TotalSeats = request.TotalSeats,
            Manufacturer = request.Manufacturer,
            YearBuilt = request.YearBuilt,
            CaptainUserId = request.CaptainUserId,
            IsActive = true
        };

        context.Boats.Add(boat);
        await context.SaveChangesAsync(cancellationToken);

        return boat.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateBoatRequest request, CancellationToken cancellationToken = default)
    {
        var boat = await context.Boats
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Boat), id);

        boat.Name = request.Name;
        boat.RegistrationNumber = request.RegistrationNumber;
        boat.TotalSeats = request.TotalSeats;
        boat.Manufacturer = request.Manufacturer;
        boat.YearBuilt = request.YearBuilt;
        boat.IsActive = request.IsActive;
        boat.CaptainUserId = request.CaptainUserId;
        boat.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<BoatResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Boats
            .AsNoTracking()
            .Where(b => b.Id == id && !b.IsDeleted)
            .Select(b => new BoatResponse(
                b.Id, b.Code, b.Name, b.RegistrationNumber, b.TotalSeats, b.Manufacturer, b.YearBuilt, b.IsActive, b.CaptainUserId))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Boat), id);

    public async Task<PaginatedList<BoatResponse>> GetPageAsync(GetBoatsRequest request, CancellationToken cancellationToken = default)
    {
        var query = context.Boats
            .AsNoTracking()
            .Where(b => !b.IsDeleted);

        if (request.IsActiveOnly.HasValue)
        {
            query = query.Where(b => b.IsActive == request.IsActiveOnly.Value);
        }

        var source = query
            .OrderBy(b => b.Code)
            .Select(b => new BoatResponse(
                b.Id, b.Code, b.Name, b.RegistrationNumber, b.TotalSeats, b.Manufacturer, b.YearBuilt, b.IsActive, b.CaptainUserId));

        return await PaginatedList<BoatResponse>.CreateAsync(source, request.PageNumber, request.PageSize, cancellationToken);
    }
}
