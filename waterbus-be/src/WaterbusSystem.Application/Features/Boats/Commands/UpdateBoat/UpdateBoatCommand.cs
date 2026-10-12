using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Boats.Commands.UpdateBoat;

public record UpdateBoatCommand(
    Guid Id,
    string Name,
    string RegistrationNumber,
    int TotalSeats,
    string Manufacturer,
    int YearBuilt,
    bool IsActive,
    Guid? CaptainUserId) : IRequest<Unit>;

public class UpdateBoatCommandValidator : AbstractValidator<UpdateBoatCommand>
{
    public UpdateBoatCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID tàu không được để trống.");

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

public class UpdateBoatCommandHandler : IRequestHandler<UpdateBoatCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateBoatCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateBoatCommand request, CancellationToken cancellationToken)
    {
        var boat = await _context.Boats
            .FirstOrDefaultAsync(b => b.Id == request.Id && !b.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Boat), request.Id);

        boat.Name = request.Name;
        boat.RegistrationNumber = request.RegistrationNumber;
        boat.TotalSeats = request.TotalSeats;
        boat.Manufacturer = request.Manufacturer;
        boat.YearBuilt = request.YearBuilt;
        boat.IsActive = request.IsActive;
        boat.CaptainUserId = request.CaptainUserId;
        boat.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
