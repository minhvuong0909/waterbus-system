using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;


namespace WaterbusSystem.Application.Features.Stations.Commands.CreateStation;

public record CreateStationCommand(
    string Code,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int OrderIndex) : IRequest<Guid>;

public class CreateStationCommandValidator : AbstractValidator<CreateStationCommand>
{
    public CreateStationCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã bến không được để trống.")
            .MaximumLength(20).WithMessage("Mã bến tối đa 20 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên bến không được để trống.")
            .MaximumLength(150).WithMessage("Tên bến tối đa 150 ký tự.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Địa chỉ tối đa 300 ký tự.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Thứ tự bến phải lớn hơn hoặc bằng 0.");
    }
}

public class CreateStationCommandHandler : IRequestHandler<CreateStationCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateStationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateStationCommand request, CancellationToken cancellationToken)
    {
        var codeExists = await _context.Stations
            .AnyAsync(s => s.Code == request.Code && !s.IsDeleted, cancellationToken);

        if (codeExists)
        {
            throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
            {
                new(nameof(request.Code), $"Mã bến '{request.Code}' đã tồn tại trong hệ thống.")
            });
        }

        var station = new Station
        {
            Code = request.Code,
            Name = request.Name,
            Address = request.Address,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            OrderIndex = request.OrderIndex,
            IsActive = true
        };

        _context.Stations.Add(station);
        await _context.SaveChangesAsync(cancellationToken);

        return station.Id;
    }
}
