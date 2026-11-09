using WaterbusSystem.Application.Features.Notifications;

namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>Cổng gửi email ra ngoài (SMTP/API provider). Ném <see cref="EmailDeliveryException"/> khi thất bại.</summary>
public interface IEmailTransport
{
    Task<EmailSendResult> SendAsync(OutboundEmail email, CancellationToken cancellationToken);
}

/// <summary>Dựng ảnh QR (PNG) từ payload đã ký.</summary>
public interface IQrImageRenderer
{
    byte[] RenderPng(string payload);
}
