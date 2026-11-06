using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Boats;
using WaterbusSystem.Application.DTOs.Responses.Boats;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Boats.Services;

public sealed class BoatService(IBoatRepository boats, IUnitOfWork unitOfWork) : IBoatService
{
    public async Task<Guid> CreateAsync(CreateBoatRequest request, CancellationToken cancellationToken = default)
    {
        if (await boats.ExistsByCodeAsync(request.Code, cancellationToken))
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

        boats.Add(boat);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return boat.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateBoatRequest request, CancellationToken cancellationToken = default)
    {
        var boat = await boats.GetRequiredByIdAsync(id, cancellationToken);

        // Không để sức chứa khai báo thấp hơn số ghế thật đang bán; không tự sinh/xoá ghế theo TotalSeats.
        var activeSeats = await boats.CountActiveSeatsAsync(id, cancellationToken);
        if (request.TotalSeats < activeSeats)
        {
            throw new ConflictException(
                $"TotalSeats ({request.TotalSeats}) thấp hơn số ghế đang hoạt động ({activeSeats}). Hãy ngừng bớt ghế trước.");
        }

        boat.Name = request.Name;
        boat.RegistrationNumber = request.RegistrationNumber;
        boat.TotalSeats = request.TotalSeats;
        boat.Manufacturer = request.Manufacturer;
        boat.YearBuilt = request.YearBuilt;
        boat.IsActive = request.IsActive;
        boat.CaptainUserId = request.CaptainUserId;
        boat.UpdatedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<BoatResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        boats.GetDetailAsync(id, cancellationToken);

    public Task<PaginatedList<BoatResponse>> GetPageAsync(GetBoatsRequest request, CancellationToken cancellationToken = default) =>
        boats.GetPageAsync(request, cancellationToken);
}
