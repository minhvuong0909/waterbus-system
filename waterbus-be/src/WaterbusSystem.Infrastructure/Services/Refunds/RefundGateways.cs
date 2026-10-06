using Microsoft.Extensions.Logging;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Infrastructure.Services.Payment;

namespace WaterbusSystem.Infrastructure.Services.Refunds;

/// <summary>
/// BR-RF-10 - nhánh 1: hoàn tiền tự động qua API merchant_webapi của VNPAY.
///
/// Lưu ý thực tế: chức năng refund của VNPAY bị HẠN CHẾ trong môi trường sandbox và đòi hỏi
/// merchant đăng ký dịch vụ để được cấp quyền trên production. Vì vậy gateway này phải trả về
/// Failed/Unavailable một cách có kiểm soát để hệ thống chuyển sang nhánh hoàn thủ công của Admin,
/// thay vì ném lỗi làm hỏng cả luồng hoàn tiền.
/// </summary>
public class VnPayRefundGateway : IRefundGateway
{
    private readonly IVnPayService _vnPayService;
    private readonly ILogger<VnPayRefundGateway> _logger;

    public VnPayRefundGateway(IVnPayService vnPayService, ILogger<VnPayRefundGateway> logger)
    {
        _vnPayService = vnPayService;
        _logger = logger;
    }

    public string Name => "VNPAY";

    public bool SupportsAutomaticRefund => true;

    public async Task<RefundGatewayResult> RefundAsync(
        RefundGatewayRequest request, CancellationToken cancellationToken)
    {
        var result = await _vnPayService.RefundAsync(
            new VnPayRefundRequest
            {
                RequestId = request.RequestId,
                MerchantReference = request.MerchantReference,
                GatewayReference = request.GatewayReference,
                Amount = request.Amount,
                CreatedBy = request.CreatedBy,
                TransactionDate = request.TransactionDate,
                IpAddress = request.IpAddress,
                OrderInfo = request.OrderInfo,
                IsFullRefund = request.IsFullRefund
            },
            cancellationToken);

        // 05 = đang xử lý, 06 = đã gửi yêu cầu hoàn sang ngân hàng -> giữ ở trạng thái chờ đối soát.
        if (result.IsSuccess && result.IsPendingProcessing)
        {
            return RefundGatewayResult.Pending(result.RefundTransactionNo);
        }

        if (result.IsSuccess)
        {
            return RefundGatewayResult.Succeeded(result.RefundTransactionNo ?? request.RequestId);
        }

        _logger.LogWarning(
            "[REFUND] VNPAY từ chối yêu cầu hoàn {Reference} (code {Code}): {Message}. Chuyển sang xử lý thủ công.",
            request.MerchantReference, result.ResponseCode, result.Message);

        return RefundGatewayResult.Failed(
            $"VNPAY trả về mã {result.ResponseCode}: {result.Message}", result.ResponseCode);
    }
}

/// <summary>
/// BR-RF-10 - nhánh 2: gateway không hỗ trợ hoặc lỗi không xử lý tự động được thì Admin hoàn tiền
/// thủ công có kiểm soát. Gateway này không tự động làm gì - nó giữ yêu cầu ở trạng thái chờ
/// cho tới khi Admin xác nhận đã chuyển khoản và cung cấp bằng chứng.
///
/// DB đã enforce điều này bằng check constraint CK_FinancialTransactions_ManualEvidence:
/// không thể có Refund với GatewayName="Manual" và Status="Succeeded" nếu thiếu
/// ManualProcessedByAdminId và ManualRefundEvidence.
/// </summary>
public class ManualRefundGateway : IRefundGateway
{
    private readonly ILogger<ManualRefundGateway> _logger;

    public ManualRefundGateway(ILogger<ManualRefundGateway> logger)
    {
        _logger = logger;
    }

    public string Name => "Manual";

    public bool SupportsAutomaticRefund => false;

    public Task<RefundGatewayResult> RefundAsync(
        RefundGatewayRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[REFUND] Yêu cầu hoàn {Amount} cho {Reference} chờ Admin xác nhận thủ công.",
            request.Amount, request.MerchantReference);

        return Task.FromResult(RefundGatewayResult.Pending());
    }
}