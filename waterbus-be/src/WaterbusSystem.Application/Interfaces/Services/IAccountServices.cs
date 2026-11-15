using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.DTOs.Responses.Accounts;

namespace WaterbusSystem.Application.Interfaces.Services;

/// <summary>
/// Đăng ký tài khoản khách hàng (Passenger) và xác minh email. Triển khai ở Infrastructure vì cần UserManager.
/// Phản hồi đăng ký và gửi lại luôn như nhau để không dò được email nào đã có tài khoản.
/// </summary>
public interface IAccountService
{
    Task<AccountRequestAcceptedResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Token sai, hết hạn hoặc không khớp tài khoản đều ném cùng một ValidationException.</summary>
    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);

    Task<AccountRequestAcceptedResponse> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default);
}

/// <summary>Hồ sơ của chính tài khoản đang đăng nhập.</summary>
public interface IProfileService
{
    Task<ProfileResponse> GetAsync(Guid accountId, CancellationToken ct = default);
    Task<ProfileResponse> UpdateAsync(Guid accountId, UpdateProfileRequest request, CancellationToken ct = default);
}

/// <summary>
/// Gắn các đơn mua khi còn là Guest vào tài khoản theo email ĐÃ XÁC MINH (chứng minh sở hữu hộp thư nhận QR).
/// Không bao giờ gắn khi email chưa xác minh, không ghi đè đơn đã thuộc tài khoản khác.
/// </summary>
public interface IOrderLinkingService
{
    Task<LinkableOrdersResponse> CountLinkableAsync(Guid accountId, CancellationToken ct = default);
    Task<LinkOrdersResponse> LinkByVerifiedEmailAsync(Guid accountId, CancellationToken ct = default);
}
