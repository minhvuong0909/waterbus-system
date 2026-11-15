using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Features.Notifications.Templates;
using WaterbusSystem.Application.Features.Tickets;

namespace WaterbusSystem.Infrastructure.Services.TicketLookup;

/// <summary>
/// Hàng đợi TRONG BỘ NHỚ cho email OTP: <see cref="Enqueue"/> trả về ngay nên thời gian phản hồi API không
/// phụ thuộc việc có gửi mail hay không (không dò được mã đặt chỗ tồn tại qua độ trễ). OTP không rời bộ nhớ tiến trình
/// (không vào DB/Hangfire). Mất khi tắt ứng dụng là chấp nhận được: OTP chỉ sống vài phút, người dùng xin lại.
/// </summary>
public sealed class TicketLookupOtpMailer(ILogger<TicketLookupOtpMailer> logger) : ITicketLookupOtpMailer
{
    internal sealed record Pending(string ToEmail, string ToName, string Otp, TimeSpan ValidFor);

    internal Channel<Pending> Queue { get; } = Channel.CreateBounded<Pending>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public void Enqueue(string toEmail, string toName, string otp, TimeSpan validFor)
    {
        if (!Queue.Writer.TryWrite(new Pending(toEmail, toName, otp, validFor)))
            logger.LogWarning("Hàng đợi email OTP đầy, bỏ một yêu cầu.");
    }
}

/// <summary>Gửi lần lượt từng OTP qua <see cref="IEmailTransport"/>. Lỗi chỉ log (không kèm địa chỉ/OTP), không thử lại.</summary>
public sealed class TicketLookupOtpMailerWorker(
    TicketLookupOtpMailer mailer,
    IServiceScopeFactory scopes,
    IOptions<EmailOptions> emailOptions,
    ILogger<TicketLookupOtpMailerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in mailer.Queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var options = emailOptions.Value;
                if (!options.Enabled)
                {
                    logger.LogWarning("Email đang tắt (Email:Enabled=false) nên không gửi được OTP tra cứu vé.");
                    continue;
                }
                var rendered = TicketLookupEmailTemplate.Render(item.ToName, item.Otp, item.ValidFor, EmailContext.From(options));
                var email = new OutboundEmail(Guid.NewGuid(), item.ToEmail, item.ToName, rendered.Subject, rendered.Html, rendered.Text, []);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailTransport>().SendAsync(email, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được email OTP tra cứu vé ({ErrorType}).", ex.GetType().Name);
            }
        }
    }
}
