using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Infrastructure.Services.Email;
using Xunit;

namespace WaterbusSystem.UnitTests.Notifications;

/// <summary>SMTP catcher cục bộ tối giản (giống Mailpit): chạy trong process, không cần Docker/credential thật.</summary>
internal sealed class SmtpCatcher : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public readonly List<byte[]> Messages = [];
    public string? RcptReply;      // ví dụ "550 5.1.1 mailbox unavailable" hoặc "451 try later"
    public string? AuthSeen;

    public SmtpCatcher() { _listener.Start(); _ = Task.Run(Loop); }

    private async Task Loop()
    {
        try { while (true) _ = Task.Run(() => Handle(_listener.AcceptTcpClient())); } catch (Exception) { /* đã dừng */ }
        await Task.CompletedTask;
    }

    private void Handle(TcpClient client)
    {
        using var _ = client; using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII); var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        writer.WriteLine("220 catcher ESMTP");
        while (reader.ReadLine() is { } line)
        {
            var cmd = line.Split(' ')[0].ToUpperInvariant();
            switch (cmd)
            {
                case "EHLO": case "HELO": writer.WriteLine("250 catcher"); break;
                case "MAIL": writer.WriteLine("250 2.1.0 OK"); break;
                case "RCPT": writer.WriteLine(RcptReply ?? "250 2.1.5 OK"); break;
                case "DATA":
                    writer.WriteLine("354 go ahead"); var data = new StringBuilder();
                    while (reader.ReadLine() is { } l && l != ".") data.AppendLine(l.StartsWith("..") ? l[1..] : l);
                    lock (Messages) Messages.Add(Encoding.ASCII.GetBytes(data.ToString()));
                    writer.WriteLine("250 2.0.0 OK queued as CATCH" + Messages.Count); break;
                case "AUTH": AuthSeen = line; writer.WriteLine("235 ok"); break;
                case "QUIT": writer.WriteLine("221 bye"); return;
                default: writer.WriteLine("250 OK"); break;
            }
        }
    }

    public MimeMessage Message(int index = 0) { lock (Messages) return MimeMessage.Load(new MemoryStream(Messages[index])); }
    public void Dispose() => _listener.Stop();
}

public sealed class MailKitTransportTests
{
    private static MailKitEmailTransport Transport(int port, Action<SmtpOptions>? tweak = null)
    {
        var smtp = new SmtpOptions { Host = "127.0.0.1", Port = port, Security = SmtpSecurity.None, AllowInsecureTransport = true, TimeoutSeconds = 5 };
        tweak?.Invoke(smtp);
        return new MailKitEmailTransport(Options.Create(smtp), Options.Create(new EmailOptions { Enabled = true, FromAddress = "no-reply@waterbus.example", FromName = "Smart Waterbus" }));
    }

    private static OutboundEmail Email(Guid? key = null, string name = "Nguyen Van A", string to = "buyer@example.com") => new(key ?? Guid.NewGuid(), to, name,
        "Vé điện tử của bạn – WB-1", "<html><body><p>Xin chào</p><img src=\"cid:qr-1@waterbus\"></body></html>", "Xin chào",
        [new InlineImage("qr-1@waterbus", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png", "qr.png")]);

    [Fact]
    public async Task DeliversAMultipartMessageToALocalSmtpCatcherWithStableIdentifiers()
    {
        using var catcher = new SmtpCatcher(); var key = Guid.NewGuid();
        var result = await Transport(catcher.Port).SendAsync(Email(key), default);

        result.ProviderMessageId.Should().Contain("queued as CATCH1");
        var message = catcher.Message();
        message.MessageId.Should().Be($"{key:N}@waterbus.example");
        message.Headers["X-Entity-Ref-ID"].Should().Be(key.ToString("N"));
        message.To.Mailboxes.Single().Address.Should().Be("buyer@example.com");
        message.From.Mailboxes.Single().Address.Should().Be("no-reply@waterbus.example");
        message.Subject.Should().Be("Vé điện tử của bạn – WB-1");
        message.HtmlBody.Should().Contain("cid:qr-1@waterbus");
        message.TextBody.Should().Contain("Xin chào");
        message.BodyParts.OfType<MimePart>().Should().Contain(p => p.ContentId == "qr-1@waterbus" && p.ContentType.MimeType == "image/png");
    }

    [Fact]
    public async Task RetryAfterAnAmbiguousFailureReusesTheSameMessageId()
    {
        using var catcher = new SmtpCatcher(); var key = Guid.NewGuid(); var transport = Transport(catcher.Port);
        await transport.SendAsync(Email(key), default); await transport.SendAsync(Email(key), default);
        catcher.Message(0).MessageId.Should().Be(catcher.Message(1).MessageId);
    }

    [Fact]
    public async Task PermanentRecipientRejectionIsNotRetryable()
    {
        using var catcher = new SmtpCatcher { RcptReply = "550 5.1.1 mailbox unavailable" };
        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(() => Transport(catcher.Port).SendAsync(Email(), default));
        ex.IsTransient.Should().BeFalse();
        catcher.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task TemporaryServerFailureIsRetryable()
    {
        using var catcher = new SmtpCatcher { RcptReply = "451 4.3.0 try again later" };
        (await Assert.ThrowsAsync<EmailDeliveryException>(() => Transport(catcher.Port).SendAsync(Email(), default))).IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task UnreachableServerIsRetryable()
    {
        var closed = new TcpListener(IPAddress.Loopback, 0); closed.Start(); var port = ((IPEndPoint)closed.LocalEndpoint).Port; closed.Stop();
        (await Assert.ThrowsAsync<EmailDeliveryException>(() => Transport(port).SendAsync(Email(), default))).IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task DisplayNameWithLineBreaksCannotInjectHeaders()
    {
        using var catcher = new SmtpCatcher();
        await Transport(catcher.Port).SendAsync(Email(name: "Evil\r\nBcc: attacker@example.com"), default);
        var message = catcher.Message();
        message.Bcc.Count.Should().Be(0); message.To.Mailboxes.Count().Should().Be(1); message.To.Mailboxes.First().Address.Should().Be("buyer@example.com");
    }

    [Fact]
    public async Task MalformedRecipientIsRejectedPermanentlyBeforeAnyNetworkCall()
    {
        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(() => Transport(1).SendAsync(Email(to: "not an address"), default));
        ex.IsTransient.Should().BeFalse();
    }

    [Fact]
    public void QrRendererProducesAPngImage()
    {
        var png = new QrCoderImageRenderer().RenderPng(new string('x', 600));
        png.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    }
}

public sealed class EmailConfigurationTests
{
    private static EmailOptions Good() => new() { Enabled = true, FromAddress = "no-reply@waterbus.example", PublicBaseUrl = "https://waterbus.example" };
    private static SmtpOptions GoodSmtp() => new() { Host = "smtp.example.com", Port = 587, Username = "user", Password = "pw" };

    [Fact] public void DisabledEmailNeedsNoConfiguration() => EmailConfigurationRules.Validate(new EmailOptions(), new SmtpOptions(), true).Should().BeEmpty();
    [Fact] public void CompleteProductionConfigurationIsAccepted() => EmailConfigurationRules.Validate(Good(), GoodSmtp(), true).Should().BeEmpty();

    [Fact]
    public void EnabledWithMissingHostAndSenderFailsValidation()
    {
        var errors = EmailConfigurationRules.Validate(new EmailOptions { Enabled = true, PublicBaseUrl = "https://x.example" }, new SmtpOptions(), true);
        errors.Should().Contain(e => e.Contains("FromAddress")).And.Contain(e => e.Contains("Smtp:Host"));
    }

    [Fact]
    public void PlaintextTransportIsRejectedInProductionAndWithoutExplicitOptIn()
    {
        var smtp = GoodSmtp(); smtp.Security = SmtpSecurity.None;
        EmailConfigurationRules.Validate(Good(), smtp, false).Should().Contain(e => e.Contains("Security=None"), "thiếu AllowInsecureTransport");
        smtp.AllowInsecureTransport = true;
        EmailConfigurationRules.Validate(Good(), smtp, true).Should().Contain(e => e.Contains("Security=None"), "Production không bao giờ cho phép");
        EmailConfigurationRules.Validate(Good(), smtp, false).Should().BeEmpty("Mailpit cục bộ được phép khi opt-in");
    }

    [Theory]
    [InlineData("user", null)] [InlineData(null, "pw")]
    public void CredentialsMustBeSuppliedAsAPair(string? user, string? password)
    {
        var smtp = GoodSmtp(); smtp.Username = user; smtp.Password = password;
        EmailConfigurationRules.Validate(Good(), smtp, true).Should().Contain(e => e.Contains("Username và Password"));
    }

    [Theory]
    [InlineData("http://waterbus.example", true)] [InlineData("not a url", false)] [InlineData("", true)]
    public void PublicBaseUrlMustBeAbsoluteAndHttpsInProduction(string url, bool production)
    {
        var options = Good(); options.PublicBaseUrl = url;
        EmailConfigurationRules.Validate(options, GoodSmtp(), production).Should().NotBeEmpty();
    }

    [Fact]
    public void LeaseMustOutliveSendTimeout()
    {
        var options = Good(); options.LeaseMinutes = 1; var smtp = GoodSmtp(); smtp.TimeoutSeconds = 60;
        EmailConfigurationRules.Validate(options, smtp, true).Should().Contain(e => e.Contains("LeaseMinutes"));
    }

    [Fact]
    public void InvalidConfigurationStopsHostStartupWhenEmailIsEnabled()
    {
        var environment = new Mock<IHostEnvironment>(); environment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var services = new ServiceCollection();
        services.AddSingleton(environment.Object);
        services.Configure<SmtpOptions>(o => { o.Security = SmtpSecurity.None; o.AllowInsecureTransport = true; o.Host = "mail"; });
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
        services.Configure<EmailOptions>(o => { o.Enabled = true; o.FromAddress = "no-reply@waterbus.example"; o.PublicBaseUrl = "https://waterbus.example"; });
        using var provider = services.BuildServiceProvider();
        var thrown = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EmailOptions>>().Value);
        thrown.Message.Should().Contain("Security=None");
    }
}
