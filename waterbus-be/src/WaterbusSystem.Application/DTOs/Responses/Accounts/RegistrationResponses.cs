namespace WaterbusSystem.Application.DTOs.Responses.Accounts;

/// <summary>Phản hồi CỐ ĐỊNH cho đăng ký / gửi lại email xác minh (không lộ email nào đã có tài khoản).</summary>
public record AccountRequestAcceptedResponse(string Message);

public record ProfileResponse(Guid Id, string Email, string FullName, string? PhoneNumber, bool EmailConfirmed);

/// <summary>Số đơn mua (khi còn là Guest) bằng email đã xác minh của tài khoản và chưa gắn tài khoản nào.</summary>
public record LinkableOrdersResponse(int Count);

/// <summary>Kết quả gắn đơn theo email đã xác minh; <paramref name="Remaining"/> &gt; 0 nghĩa là còn đơn, gọi lại để gắn tiếp.</summary>
public record LinkOrdersResponse(int Linked, int Remaining);
