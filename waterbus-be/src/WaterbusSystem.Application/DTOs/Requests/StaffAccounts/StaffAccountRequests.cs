namespace WaterbusSystem.Application.DTOs.Requests.StaffAccounts;

/// <summary>Admin tạo tài khoản Staff: không nhập mật khẩu, nhân viên tự đặt qua email kích hoạt.</summary>
public record CreateStaffAccountRequest(string FullName, string Email, string Phone, string? StaffCode = null);

public record UpdateStaffAccountRequest(string FullName, string Email, string Phone, string? StaffCode = null);

/// <summary>Bật/tắt đăng nhập. Chỉ bật được tài khoản đã kích hoạt qua email.</summary>
public record ChangeStaffAccountStatusRequest(bool IsActive);

public record ResendStaffInvitationRequest(string Reason);

public record GetStaffAccountsRequest(int Page = 1, int PageSize = 20, string? Search = null, bool? IsActive = null);
