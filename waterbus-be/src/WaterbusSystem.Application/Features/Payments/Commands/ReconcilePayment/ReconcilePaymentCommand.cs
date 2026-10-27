using MediatR;
using WaterbusSystem.Application.DTOs.Responses.Payments;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Payments.Commands.ReconcilePayment;

public record ReconcilePaymentCommand(Guid OrderId, Guid PaymentId, string? AccessToken, string IpAddress) : IRequest<VnPayIpnResultDto>;
public sealed class ReconcilePaymentCommandHandler(IPaymentService service) : IRequestHandler<ReconcilePaymentCommand, VnPayIpnResultDto>
{
    public Task<VnPayIpnResultDto> Handle(ReconcilePaymentCommand request, CancellationToken ct) => service.ReconcileAsync(request.OrderId, request.PaymentId, request.AccessToken, request.IpAddress, ct);
}
