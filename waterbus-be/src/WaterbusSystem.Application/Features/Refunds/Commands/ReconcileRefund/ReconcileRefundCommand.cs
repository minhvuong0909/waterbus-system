using MediatR;
using FluentValidation;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Application.Features.Refunds.Commands.ReconcileRefund;

public record ReconcileRefundCommand(Guid RefundId, string IpAddress) : IRequest<string>;

public sealed class ReconcileRefundCommandHandler(IRefundReconciliationService service) : IRequestHandler<ReconcileRefundCommand, string>
{
    public Task<string> Handle(ReconcileRefundCommand request, CancellationToken ct) => service.ReconcileAsync(request.RefundId, request.IpAddress, ct);
}
