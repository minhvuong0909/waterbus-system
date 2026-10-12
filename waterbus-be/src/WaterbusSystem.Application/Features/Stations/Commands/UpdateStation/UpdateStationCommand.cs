using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Stations.Commands.UpdateStation;

public record UpdateStationCommand(
    Guid Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int OrderIndex,
    bool IsActive) : IRequest<Unit>;

public class UpdateStationCommandValidator : AbstractValidator<UpdateStationCommand>
{
    public UpdateStationCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID bến không được để trống.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên bến không được để trống.")
            .MaximumLength(150).WithMessage("Tên bến tối đa 150 ký tự.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Địa chỉ tối đa 300 ký tự.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Thứ tự bến phải lớn hơn hoặc bằng 0.");
    }
}

public class UpdateStationCommandHandler : IRequestHandler<UpdateStationCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateStationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateStationCommand request, CancellationToken cancellationToken)
    {
        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Station), request.Id);

        station.Name = request.Name;
        station.Address = request.Address;
        station.Latitude = request.Latitude;
        station.Longitude = request.Longitude;
        station.OrderIndex = request.OrderIndex;
        station.IsActive = request.IsActive;
        station.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
