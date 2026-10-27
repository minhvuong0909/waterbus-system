using MediatR;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Bookings.Queries.GetCheckout;

public record GetCheckoutQuery(Guid OrderId, string? AccessToken) : IRequest<CheckoutDetailDto>;
public sealed class GetCheckoutQueryHandler(ICheckoutReadService service) : IRequestHandler<GetCheckoutQuery, CheckoutDetailDto>
{
    public Task<CheckoutDetailDto> Handle(GetCheckoutQuery request, CancellationToken ct) => service.GetAsync(request.OrderId, request.AccessToken, ct);
}
