using FluentValidation;
using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Application.Features.AiBooking.Validators;

/// <summary>
/// Validator kiểm tra tính hợp lệ của request chat gửi tới AI Assistant.
/// </summary>
public class AiBookingChatRequestValidator : AbstractValidator<AiBookingChatRequest>
{
    public AiBookingChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Nội dung tin nhắn không được để trống.")
            .MaximumLength(1000).WithMessage("Tin nhắn vượt quá độ dài tối đa 1000 ký tự.");

        RuleFor(x => x.LanguageCode)
            .Must(code => code is null or "vi" or "en")
            .WithMessage("Ngôn ngữ chỉ được là 'vi' hoặc 'en'.");

        RuleFor(x => x.History).NotNull().Must(history => history is { Count: <= 8 })
            .WithMessage("Lịch sử hội thoại tối đa 8 lượt.");
        RuleForEach(x => x.History).ChildRules(turn =>
        {
            turn.RuleFor(x => x.Role).Must(role => role is "user" or "assistant");
            turn.RuleFor(x => x.Content).NotEmpty().MaximumLength(1000);
        });

        When(x => !string.IsNullOrEmpty(x.ConversationId), () =>
        {
            RuleFor(x => x.ConversationId)
                .MaximumLength(100).WithMessage("Mã hội thoại không được vượt quá 100 ký tự.");
        });

        When(x => !string.IsNullOrEmpty(x.PreferredServiceType), () =>
        {
            RuleFor(x => x.PreferredServiceType)
                .Must(type => type == "Commuter" || type == "Sightseeing")
                .WithMessage("Loại chuyến chỉ được là 'Commuter' hoặc 'Sightseeing'.");
        });

        When(x => x.SearchContext is not null, () =>
        {
            RuleFor(x => x.SearchContext!.DepartureStationName).MaximumLength(100);
            RuleFor(x => x.SearchContext!.ArrivalStationName).MaximumLength(100);
            RuleFor(x => x.SearchContext!).SetValidator(new TripSearchCriteriaValidator());
        });

        When(x => x.PendingClarification is not null, () =>
        {
            RuleFor(x => x.PendingClarification!.OriginalMessage)
                .NotEmpty().MaximumLength(1000);
        });
    }
}
