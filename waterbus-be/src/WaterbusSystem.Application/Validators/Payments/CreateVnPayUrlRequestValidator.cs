using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Payments;

namespace WaterbusSystem.Application.Validators.Payments;

/// <summary>Chỉ áp cho create-vnpay-url. IPN, return và reconcile của VNPAY đọc query/route nên không đi qua validator này.</summary>
public class CreateVnPayUrlRequestValidator : AbstractValidator<CreateVnPayUrlRequest>
{
    public CreateVnPayUrlRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("Mã đơn hàng không được để trống.");
    }
}
