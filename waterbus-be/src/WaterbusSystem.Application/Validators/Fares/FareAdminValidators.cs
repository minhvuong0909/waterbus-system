using FluentValidation;
using WaterbusSystem.Application.DTOs.Requests.Fares;

namespace WaterbusSystem.Application.Validators.Fares;

internal static class FareRules
{
    public static readonly string[] TripTypes = ["Commuter", "Sightseeing"];
    public const decimal MaxPrice = 9_999_999_999.99m; // decimal(12,2)
    public static bool HasAtMostTwoDecimals(decimal v) => decimal.Round(v, 2) == v;
}

public class CreateFareRuleRequestValidator : AbstractValidator<CreateFareRuleRequest>
{
    public CreateFareRuleRequestValidator()
    {
        RuleFor(x => x.TripType).Must(t => FareRules.TripTypes.Contains(t)).WithMessage("TripType phải là Commuter hoặc Sightseeing.");
        RuleFor(x => x.SeatClassId).NotEmpty();
        RuleFor(x => x.Price).InclusiveBetween(0m, FareRules.MaxPrice).Must(FareRules.HasAtMostTwoDecimals).WithMessage("Giá tối đa 2 chữ số thập phân.");
        RuleFor(x => x.Currency).Equal("VND").WithMessage("Hệ thống chỉ hỗ trợ VND.");
        RuleFor(x => x.EffectiveFrom).NotEmpty();
        RuleFor(x => x.EffectiveTo).GreaterThan(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue);
    }
}

public class UpdateFareRuleRequestValidator : AbstractValidator<UpdateFareRuleRequest>
{
    public UpdateFareRuleRequestValidator()
    {
        RuleFor(x => x.Price).InclusiveBetween(0m, FareRules.MaxPrice).Must(FareRules.HasAtMostTwoDecimals).WithMessage("Giá tối đa 2 chữ số thập phân.");
        RuleFor(x => x.EffectiveFrom).NotEmpty();
        RuleFor(x => x.EffectiveTo).GreaterThan(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue);
    }
}

public class GetFareRulesRequestValidator : AbstractValidator<GetFareRulesRequest>
{
    public GetFareRulesRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.TripType).Must(t => t is null || FareRules.TripTypes.Contains(t));
    }
}
