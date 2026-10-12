using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;


namespace WaterbusSystem.Application.Features.Boats.Commands.CreateBoat;

public record CreateBoatCommand(
    string Code,
    string Name,
    string RegistrationNumber,
    int TotalSeats,
    string Manufacturer,
    int YearBuilt,
    Guid? CaptainUserId) : IRequest<Guid>;

public class CreateBoatCommandValidator : AbstractValidator<CreateBoatCommand>
{
    public CreateBoatCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã tàu không được để trống.")
            .MaximumLength(20).WithMessage("Mã tàu tối đa 20 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên tàu không được để trống.")
            .MaximumLength(100).WithMessage("Tên tàu tối đa 100 ký tự.");

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Số đăng kiểm không được để trống.")
            .MaximumLength(50).WithMessage("Số đăng kiểm tối đa 50 ký tự.");

        RuleFor(x => x.TotalSeats)
            .GreaterThan(0).WithMessage("Tổng số ghế phải lớn hơn 0.");

        RuleFor(x => x.YearBuilt)
            .GreaterThan(1900).LessThanOrEqualTo(DateTime.UtcNow.Year + 1)
            .WithMessage("Năm đóng tàu không hợp lệ.");
    }
}

public class CreateBoatCommandHandler : IRequestHandler<CreateBoatCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateBoatCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateBoatCommand request, CancellationToken cancellationToken)
    {
        var codeExists = await _context.Boats
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

        _context.Boats.Add(boat);
        await _context.SaveChangesAsync(cancellationToken);

        return boat.Id;
    }
}
