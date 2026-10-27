using MediatR;
using WaterbusSystem.Application.DTOs.Responses.Payments;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Payments.Queries.GetVnPayReturn;

public record GetVnPayReturnQuery(IReadOnlyDictionary<string, string> Query) : IRequest<VnPayReturnDto>;
public sealed class GetVnPayReturnQueryHandler(IPaymentService service) : IRequestHandler<GetVnPayReturnQuery, VnPayReturnDto>
{
    public Task<VnPayReturnDto> Handle(GetVnPayReturnQuery request, CancellationToken ct) => service.GetReturnAsync(request.Query, ct);
}
