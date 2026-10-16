using MediatR;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

using WaterbusSystem.Application.Features.Payments.Services;

namespace WaterbusSystem.Application.Features.Payments.Commands.ConfirmVnPayIpn;

public record VnPayIpnResultDto([property: JsonPropertyName("RspCode")] string RspCode,
    [property: JsonPropertyName("Message")] string Message);
public record ConfirmVnPayIpnCommand(IReadOnlyDictionary<string, string> Query) : IRequest<VnPayIpnResultDto>;

public sealed class ConfirmVnPayIpnCommandHandler(PaymentConfirmationService confirmation)
    : IRequestHandler<ConfirmVnPayIpnCommand, VnPayIpnResultDto>
{
    public Task<VnPayIpnResultDto> Handle(ConfirmVnPayIpnCommand request, CancellationToken ct) =>
        confirmation.ConfirmIpnAsync(request, ct);
}
