using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.SeatInventory;

namespace WaterbusSystem.Application.Validators.SeatInventory;

public class CreateSeatClassRequestValidator : AbstractValidator<CreateSeatClassRequest>
{
    public CreateSeatClassRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateSeatClassRequestValidator : AbstractValidator<UpdateSeatClassRequest>
{
    public UpdateSeatClassRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class SeatInputValidator : AbstractValidator<SeatInput>
{
    public SeatInputValidator()
    {
        RuleFor(x => x.SeatCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.SeatClassId).NotEmpty();
        RuleFor(x => x.RowNumber).InclusiveBetween(1, 100);
        RuleFor(x => x.ColumnNumber).InclusiveBetween(1, 100);
    }
}

public class AddBoatSeatsRequestValidator : AbstractValidator<AddBoatSeatsRequest>
{
    public AddBoatSeatsRequestValidator()
    {
        RuleFor(x => x.Seats).NotNull().NotEmpty().Must(s => s is null || s.Count <= 200)
            .WithMessage("Mỗi lần chỉ thêm tối đa 200 ghế.");
        RuleForEach(x => x.Seats).SetValidator(new SeatInputValidator());
    }
}

public class UpdateBoatSeatRequestValidator : AbstractValidator<UpdateBoatSeatRequest>
{
    public UpdateBoatSeatRequestValidator()
    {
        RuleFor(x => x.SeatCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.SeatClassId).NotEmpty();
        RuleFor(x => x.RowNumber).InclusiveBetween(1, 100);
        RuleFor(x => x.ColumnNumber).InclusiveBetween(1, 100);
    }
}
