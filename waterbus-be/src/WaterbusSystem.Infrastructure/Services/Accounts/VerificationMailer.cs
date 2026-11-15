using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Accounts;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Features.Notifications.Templates;

namespace WaterbusSystem.Infrastructure.Services.Accounts;

/// <summary>
/// Hàng đợi trong bộ nhớ cho email xác minh: <see cref="TryEnqueue"/> trả về ngay nên thời gian phản hồi không lộ email nào
/// tồn tại. Liên kết xác minh không rời bộ nhớ tiến trình (không vào DB/Hangfire). Mất khi tắt ứng dụng là chấp nhận được:
/// người dùng bấm "Gửi lại email xác minh".
/// </summary>
public sealed class VerificationMailer(ILogger<VerificationMailer> logger) : IVerificationEmailQueue
{
    internal sealed record Pending(string ToEmail, string ToName, string Link, double? InvitationValidHours = null);

    internal Channel<Pending> Queue { get; } = Channel.CreateBounded<Pending>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    public bool TryEnqueue(string toEmail, string toName, string verificationLink)
        => Enqueue(new Pending(toEmail, toName, verificationLink));

    public bool TryEnqueueInvitation(string toEmail, string toName, string activationLink, double validHours)
        => Enqueue(new Pending(toEmail, toName, activationLink, validHours));

    private bool Enqueue(Pending item)
    {
        if (Queue.Writer.TryWrite(item)) return true;

        logger.LogWarning("Hàng đợi email xác minh không nhận thêm yêu cầu.");
        return false;
    }
}

/// <summary>Gửi lần lượt qua <see cref="IEmailTransport"/>. Lỗi chỉ log (không kèm địa chỉ/liên kết), không thử lại.</summary>
public sealed class VerificationMailerWorker(
    VerificationMailer mailer,
    IServiceScopeFactory scopes,
    IOptions<EmailOptions> emailOptions,
    ILogger<VerificationMailerWorker> logger) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!emailOptions.Value.Enabled)
            logger.LogWarning("Email:Enabled=false: email xác minh tài khoản sẽ KHÔNG được gửi, khách không đăng nhập được. Bật Email:Enabled và cấu hình SMTP.");
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in mailer.Queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var options = emailOptions.Value;
                if (!options.Enabled) continue;
                var rendered = item.InvitationValidHours is { } hours
                    ? AccountEmailTemplates.PersonnelInvitation(item.ToName, item.Link, hours, EmailContext.From(options))
                    : AccountEmailTemplates.Verification(item.ToName, item.Link, EmailContext.From(options));
                var email = new OutboundEmail(Guid.NewGuid(), item.ToEmail, item.ToName, rendered.Subject, rendered.Html, rendered.Text, []);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailTransport>().SendAsync(email, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Không gửi được email xác minh tài khoản ({ErrorType}).", ex.GetType().Name);
            }
        }
    }
}
