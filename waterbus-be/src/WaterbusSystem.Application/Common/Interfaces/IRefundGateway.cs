namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// BR-RF-10: khi gateway không hỗ trợ hoàn tiền tự động, hoặc trả lỗi không xử lý được,
/// Admin thực hiện quy trình hoàn tiền thủ công có kiểm soát. Abstraction này cho phép hệ thống
/// chạy được và demo được trước khi được cấp quyền API hoàn tiền của nhà cung cấp.
/// </summary>
public interface IRefundGateway
{
    /// <summary>Tên lưu vào FinancialTransaction.GatewayName để đối soát (ví dụ "VNPAY", "Manual").</summary>
    string Name { get; }

    /// <summary>Gateway có tự động hoàn về phương thức thanh toán gốc không.</summary>
    bool SupportsAutomaticRefund { get; }

    /// <summary>
    /// Gửi yêu cầu hoàn tiền. Gateway KHÔNG được tự ý quyết định đơn vị là hoàn thành: nếu chưa chắc
    /// chắn thì trả <see cref="RefundGatewayResult.Pending"/> để hệ thống giữ ở trạng thái chờ đối soát.
    /// </summary>
    Task<RefundGatewayResult> RefundAsync(RefundGatewayRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Yêu cầu hoàn tiền gửi tới gateway.</summary>
public sealed record RefundGatewayRequest
{
    /// <summary>Mã tham chiếu giao dịch gốc đã gửi lên cổng thanh toán (vnp_TxnRef).</summary>
    public required string MerchantReference { get; init; }

    /// <summary>Mã giao dịch ghi nhận tại hệ thống gateway (vnp_TransactionNo), có thể rỗng.</summary>
    public string? GatewayReference { get; init; }

    /// <summary>Mã hệ thống merchant tự sinh, duy nhất cho mỗi lần gửi yêu cầu hoàn.</summary>
    public required string RequestId { get; init; }

    /// <summary>Ngày giờ giao dịch gốc tại hệ thống merchant (GMT+7, yyyyMMddHHmmss).</summary>
    public required string TransactionDate { get; init; }

    /// <summary>Số tiền hoàn, tính theo paid allocation đã phân bổ cho các Ticket đủ điều kiện.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Người khởi tạo yêu cầu hoàn (BR-RF-10 yêu cầu lưu người thực hiện).</summary>
    public required string CreatedBy { get; init; }

    /// <summary>Địa chỉ IP máy chủ gọi API.</summary>
    public required string IpAddress { get; init; }

    /// <summary>Mô tả lý do hoàn để đối soát.</summary>
    public required string OrderInfo { get; init; }

    /// <summary>true = hoàn toàn phần, false = hoàn một phần (BR-PAY: 03 = partial).</summary>
    public bool IsFullRefund { get; init; }
}

/// <summary>Kết quả một lần gọi gateway hoàn tiền.</summary>
public sealed record RefundGatewayResult
{
    public required RefundGatewayOutcome Outcome { get; init; }
    public string? GatewayReference { get; init; }
    public string? FailureReason { get; init; }
    public string? ResponseCode { get; init; }

    /// <summary>Gateway đã nhận yêu cầu nhưng chưa xong - cần polling QueryDr để đối soát.</summary>
    public static RefundGatewayResult Pending(string? gatewayReference = null) =>
        new() { Outcome = RefundGatewayOutcome.Pending, GatewayReference = gatewayReference };

    /// <summary>Gateway đã xác nhận hoàn thành.</summary>
    public static RefundGatewayResult Succeeded(string gatewayReference) =>
        new() { Outcome = RefundGatewayOutcome.Succeeded, GatewayReference = gatewayReference };

    /// <summary>Gateway từ chối hoặc lỗi - chuyển sang quy trình hoàn thủ công của Admin.</summary>
    public static RefundGatewayResult Failed(string reason, string? responseCode = null) =>
        new() { Outcome = RefundGatewayOutcome.Failed, FailureReason = reason, ResponseCode = responseCode };
}

public enum RefundGatewayOutcome
{
    /// <summary>Chưa xác định được, phải chờ đối soát.</summary>
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}