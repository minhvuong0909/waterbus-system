using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Services.Accounts;
using Xunit;

namespace WaterbusSystem.UnitTests.Accounts;

public class VerificationMailerTests
{
    [Fact]
    public async Task FullQueueReportsRejectionAndAcceptsAgainAfterDraining()
    {
        var log = new CaptureWarning<VerificationMailer>();
        var mailer = new VerificationMailer(log);
        for (var index = 0; index < 200; index++)
            Assert.True(mailer.TryEnqueue("buyer@example.test", "Buyer", "link"));

        Assert.False(mailer.TryEnqueue("buyer@example.test", "Buyer", "rejected-token"));
        var warning = await log.Warning.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain("rejected-token", warning.Message);
        Assert.True(mailer.Queue.Reader.TryRead(out _));
        Assert.True(mailer.TryEnqueue("buyer@example.test", "Buyer", "new-link"));
    }

    [Fact]
    public async Task TransportFailureIsLoggedWithoutPayload_AndResendStillDelivers()
    {
        const string email = "buyer@example.test";
        const string firstToken = "initial-token";
        var transport = new Mock<IEmailTransport>();
        var delivered = new TaskCompletionSource<OutboundEmail>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.SetupSequence(t => t.SendAsync(It.IsAny<OutboundEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException($"SMTP rejected {email} and {firstToken}", isTransient: false));
        transport.Setup(t => t.SendAsync(It.Is<OutboundEmail>(m => !m.TextBody.Contains(firstToken)), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutboundEmail message, CancellationToken _) =>
            {
                delivered.TrySetResult(message);
                return new EmailSendResult("resend");
            });

        await using var services = new ServiceCollection().AddSingleton(transport.Object).BuildServiceProvider();
        var options = Options.Create(new EmailOptions { Enabled = true, PublicBaseUrl = "https://waterbus.example" });
        var mailer = new VerificationMailer(NullLogger<VerificationMailer>.Instance);
        var log = new CaptureWarning<VerificationMailerWorker>();
        using var worker = new VerificationMailerWorker(mailer, services.GetRequiredService<IServiceScopeFactory>(), options, log);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(mailer.TryEnqueue(email, "Buyer", $"https://waterbus.example/verify-email#token={firstToken}"));
            var warning = await log.Warning.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(warning.Exception);
            Assert.DoesNotContain(email, warning.Message);
            Assert.DoesNotContain(firstToken, warning.Message);
            Assert.Contains(nameof(EmailDeliveryException), warning.Message);

            var user = new ApplicationUser { Email = email, FullName = "Buyer", IsActive = true };
            var users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            users.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(user);
            users.Setup(u => u.IsInRoleAsync(user, AccountService.PassengerRole)).ReturnsAsync(true);
            users.Setup(u => u.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("new-token");
            var cooldown = new Mock<IEmailCooldownStore>();
            cooldown.Setup(c => c.TryAcquireAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var account = new AccountService(users.Object, Mock.Of<IUnitOfWork>(), mailer, cooldown.Object, options, NullLogger<AccountService>.Instance, TimeSpan.Zero);
            await account.ResendVerificationAsync(new ResendVerificationRequest(email));

            var sent = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(email, sent.ToEmail);
            Assert.Contains("/verify-email#uid=", sent.TextBody);
            Assert.DoesNotContain(firstToken, sent.TextBody);
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }

    private sealed class CaptureWarning<T> : ILogger<T>
    {
        public TaskCompletionSource<(string Message, Exception? Exception)> Warning { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) Warning.TrySetResult((formatter(state, exception), exception));
        }
    }
}
