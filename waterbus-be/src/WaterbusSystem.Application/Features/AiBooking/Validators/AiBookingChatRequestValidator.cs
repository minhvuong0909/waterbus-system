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
    }
}
