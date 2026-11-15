using WaterbusSystem.Application.DTOs.Requests.Accounts;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>Dùng chung cho nhân sự do Admin mời (Captain/Staff), tái sử dụng màn hình xác minh email hiện có.</summary>
public interface IPersonnelInvitationService
{
    void EnsureDeliveryConfigured();
    Task SendAsync(Guid userId, string reason, CancellationToken ct = default);
    Task AcceptAsync(ConfirmEmailRequest request, CancellationToken ct = default);
}
