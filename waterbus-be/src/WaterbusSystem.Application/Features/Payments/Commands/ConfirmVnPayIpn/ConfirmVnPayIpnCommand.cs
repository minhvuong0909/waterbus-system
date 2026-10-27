using MediatR;
using WaterbusSystem.Application.DTOs.Responses.Payments;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;

public record ConfirmVnPayIpnCommand(IReadOnlyDictionary<string, string> Query) : IRequest<VnPayIpnResultDto>;
public sealed class ConfirmVnPayIpnCommandHandler(IPaymentService service) : IRequestHandler<ConfirmVnPayIpnCommand, VnPayIpnResultDto>
{
    public Task<VnPayIpnResultDto> Handle(ConfirmVnPayIpnCommand request, CancellationToken ct) => service.ConfirmIpnAsync(request.Query, ct);
}
