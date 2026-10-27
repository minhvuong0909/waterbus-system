using MediatR;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Bookings.Commands.ExpireCheckoutHolds;

public record ExpireCheckoutHoldsCommand : IRequest<int>;
public sealed class ExpireCheckoutHoldsCommandHandler(ICheckoutExpirationService service) : IRequestHandler<ExpireCheckoutHoldsCommand, int>
{
    public Task<int> Handle(ExpireCheckoutHoldsCommand request, CancellationToken ct) => service.ExpireAsync(ct);
}
