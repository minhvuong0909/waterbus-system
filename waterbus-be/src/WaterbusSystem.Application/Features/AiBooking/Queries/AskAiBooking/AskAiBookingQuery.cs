using FluentValidation;
using MediatR;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;

namespace WaterbusSystem.Application.Features.AiBooking.Queries.AskAiBooking;

/// <summary>Gửi yêu cầu trò chuyện tới AI Booking Assistant.</summary>
public record AskAiBookingQuery(AiBookingChatRequest Request) : IRequest<AiBookingChatResponse>;

public sealed class AskAiBookingQueryValidator : AbstractValidator<AskAiBookingQuery>
{
    public AskAiBookingQueryValidator()
    {
        RuleFor(x => x.Request).NotNull();
    }
}

public sealed class AskAiBookingQueryHandler : IRequestHandler<AskAiBookingQuery, AiBookingChatResponse>
{
    private readonly IAiBookingAssistantService _assistant;

    public AskAiBookingQueryHandler(IAiBookingAssistantService assistant) => _assistant = assistant;

    public Task<AiBookingChatResponse> Handle(AskAiBookingQuery request, CancellationToken cancellationToken) =>
        _assistant.ProcessChatAsync(request.Request, cancellationToken);
}
