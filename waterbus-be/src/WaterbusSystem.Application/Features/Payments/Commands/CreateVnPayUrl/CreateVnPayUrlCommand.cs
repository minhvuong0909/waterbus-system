using MediatR;
using WaterbusSystem.Application.DTOs.Responses.Payments;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Payments.Commands.CreateVnPayUrl;

public record CreateVnPayUrlCommand(Guid OrderId, string IpAddress, string? AccessToken = null) : IRequest<CreateVnPayUrlResponseDto>;
public sealed class CreateVnPayUrlCommandHandler(IPaymentService service) : IRequestHandler<CreateVnPayUrlCommand, CreateVnPayUrlResponseDto>
{
    public Task<CreateVnPayUrlResponseDto> Handle(CreateVnPayUrlCommand request, CancellationToken ct) => service.CreateVnPayUrlAsync(request.OrderId, request.IpAddress, request.AccessToken, ct);
}
