using MediatR;
using FluentValidation;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Refunds.Commands.RequestRefund;

public record RequestRefundCommand(
    Guid BookingId,
    string Reason,
    string? IpAddress = null,
    string? CreatedBy = null) : IRequest<RequestRefundResultDto>;
public sealed class RequestRefundCommandValidator : AbstractValidator<RequestRefundCommand>
{
    public RequestRefundCommandValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}

public sealed class RequestRefundCommandHandler(IRefundRequestService service) : IRequestHandler<RequestRefundCommand, RequestRefundResultDto>
{
    public Task<RequestRefundResultDto> Handle(RequestRefundCommand request, CancellationToken ct) => service.RequestAsync(new(request.BookingId, request.Reason, request.IpAddress, request.CreatedBy), ct);
}
