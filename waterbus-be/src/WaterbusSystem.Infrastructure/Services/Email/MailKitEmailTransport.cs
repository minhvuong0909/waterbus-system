using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using QRCoder;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Notifications;

namespace WaterbusSystem.Infrastructure.Services.Email;

/// <summary>
/// Transport SMTP (MailKit). Phân loại lỗi: tạm thời (retry) vs bị từ chối vĩnh viễn (DeadLetter).
/// Message-Id ổn định theo outbox id: retry sau timeout mơ hồ gửi cùng Message-Id để MTA/MUA có thể khử trùng.
/// Không log nội dung/địa chỉ người nhận.
/// </summary>
public sealed class MailKitEmailTransport(IOptions<SmtpOptions> smtpOptions, IOptions<EmailOptions> emailOptions) : IEmailTransport
{
    public async Task<EmailSendResult> SendAsync(OutboundEmail email, CancellationToken ct)
    {
        var smtp = smtpOptions.Value;
        var message = Build(email, emailOptions.Value);
        using var client = new SmtpClient { Timeout = smtp.TimeoutSeconds * 1000 };
        try
        {
            await client.ConnectAsync(smtp.Host, smtp.Port, smtp.Security switch
            {
                SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
                SmtpSecurity.None => SecureSocketOptions.None,
                _ => SecureSocketOptions.StartTls // bắt buộc TLS, không hạ cấp ngầm
            }, ct);
            if (!string.IsNullOrWhiteSpace(smtp.Username))
                await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);
            var response = await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, CancellationToken.None);
            var id = string.IsNullOrWhiteSpace(response) ? message.MessageId : response.Trim();
            return new EmailSendResult(id.Length > 200 ? id[..200] : id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (SmtpCommandException ex) when (IsPermanent(ex))
        {
            throw new EmailDeliveryException($"SMTP {(int)ex.StatusCode} {ex.ErrorCode}", false, ex);
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or AuthenticationException
            or ServiceNotConnectedException or ServiceNotAuthenticatedException or IOException or SocketException
            or TimeoutException or OperationCanceledException)
        {
            // Gồm cả sai thông tin xác thực: là lỗi cấu hình có thể sửa được nên retry có chặn thay vì bỏ ngay.
            throw new EmailDeliveryException($"SMTP transient failure: {ex.GetType().Name}", true, ex);
        }
    }

    // 5xx là từ chối vĩnh viễn, trừ nhóm lỗi xác thực (530/534/535) vốn do cấu hình và sửa được.
    private static bool IsPermanent(SmtpCommandException ex) =>
        (int)ex.StatusCode >= 500 && (int)ex.StatusCode is not (530 or 534 or 535);

    internal static MimeMessage Build(OutboundEmail email, EmailOptions options)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
            message.To.Add(new MailboxAddress(StripControl(email.ToName), email.ToEmail));
            message.Subject = email.Subject;
            var domain = options.FromAddress[(options.FromAddress.LastIndexOf('@') + 1)..];
            message.MessageId = $"{email.MessageKey:N}@{domain}";
            message.Headers.Add("X-Entity-Ref-ID", email.MessageKey.ToString("N"));
            var body = new BodyBuilder { HtmlBody = email.HtmlBody, TextBody = email.TextBody };
            foreach (var image in email.Images)
            {
                var part = body.LinkedResources.Add(image.FileName, image.Content, ContentType.Parse(image.MediaType));
                part.ContentId = image.ContentId;
            }
            message.Body = body.ToMessageBody();
            return message;
        }
        catch (Exception ex) when (ex is ParseException or FormatException or ArgumentException)
        {
            throw new EmailDeliveryException("Invalid address or message content: " + ex.GetType().Name, false, ex);
        }
    }

    private static string StripControl(string value) => new(value.Where(c => !char.IsControl(c)).ToArray());
}

public sealed class QrCoderImageRenderer : IQrImageRenderer
{
    public byte[] RenderPng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(6);
    }
}
