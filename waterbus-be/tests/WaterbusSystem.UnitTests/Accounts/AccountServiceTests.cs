using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Application.Interfaces.Persistence;
using Xunit;

namespace WaterbusSystem.UnitTests.Accounts;

/// <summary>Đăng ký khách hàng, xác minh email, gửi lại: đúng role, không lộ email tồn tại, không gửi spam.</summary>
public class AccountServiceTests
{
    private const string Email = "customer@gmail.com";

    private sealed class CapturingQueue : IVerificationEmailQueue
    {
        public readonly List<(string Email, string Name, string Link)> Sent = new();
        public bool TryEnqueue(string toEmail, string toName, string verificationLink)
        {
            Sent.Add((toEmail, toName, verificationLink));
            return true;
        }
    }

    private sealed class Harness
    {
        public readonly Mock<UserManager<ApplicationUser>> Users;
        public readonly CapturingQueue Queue = new();
        public readonly Mock<IEmailCooldownStore> Cooldown = new();
        public readonly AccountService Sut;

        public Harness(string? publicBaseUrl = "https://waterbus.example")
        {
            Users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            Users.Object.PasswordHasher = Mock.Of<IPasswordHasher<ApplicationUser>>();
            Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            Users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Passenger")).ReturnsAsync(IdentityResult.Success);
            Users.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("raw+token/with=chars");
            Cooldown.Setup(c => c.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Sut = new AccountService(Users.Object, Mock.Of<IUnitOfWork>(), Queue, Cooldown.Object,
                Microsoft.Extensions.Options.Options.Create(new EmailOptions { PublicBaseUrl = publicBaseUrl }),
                NullLogger<AccountService>.Instance, minResponseTime: TimeSpan.Zero);
        }
    }

    private static RegisterRequest Register(string email = Email) =>
        new(email, "Nguyen Van A", "0901234567");

    // ---------- Đăng ký ----------

    [Fact]
    public async Task Register_NewEmail_ShouldCreateUnverifiedPassengerAndQueueVerificationEmail()
    {
        var h = new Harness();

        await h.Sut.RegisterAsync(Register());

        h.Users.Verify(u => u.CreateAsync(It.Is<ApplicationUser>(x => x.Email == Email && x.EmailConfirmed == false && x.IsActive && x.PasswordHash == null)), Times.Once);
        h.Users.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Passenger"), Times.Once);
        var sent = h.Queue.Sent.Should().ContainSingle().Subject;
        sent.Email.Should().Be(Email);
        sent.Link.Should().StartWith("https://waterbus.example/verify-email#uid=").And.Contain("&token=");
    }

    [Fact]
    public async Task Register_ShouldAlwaysAssignOnlyThePassengerRole()
    {
        var h = new Harness();

        await h.Sut.RegisterAsync(Register());

        h.Users.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.Is<string>(r => r != "Passenger")), Times.Never);
    }

    [Fact]
    public async Task Register_ExistingEmail_ShouldCreateNothingSendNothingAndRespondTheSame()
    {
        var fresh = await new Harness().Sut.RegisterAsync(Register());
        var h = new Harness();
        h.Users.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(new ApplicationUser { Email = Email });

        var existing = await h.Sut.RegisterAsync(Register());

        existing.Should().Be(fresh);
        h.Users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        h.Queue.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WhenAnotherRequestWonTheRace_ShouldTreatItAsExistingEmail()
    {
        var h = new Harness();
        h.Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail" }));

        var act = () => h.Sut.RegisterAsync(Register());

        await act.Should().NotThrowAsync();
        h.Queue.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WhenPassengerRoleCannotBeAssigned_ShouldRemoveTheNewAccountAndFail()
    {
        var h = new Harness();
        h.Users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Passenger"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "RoleNotFound" }));
        h.Users.Setup(u => u.DeleteAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var act = () => h.Sut.RegisterAsync(Register());

        await act.Should().ThrowAsync<InvalidOperationException>();
        h.Users.Verify(u => u.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Once);
        h.Queue.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WithoutPublicBaseUrl_ShouldNotQueueAnEmailWithABrokenLink()
    {
        var h = new Harness(publicBaseUrl: null);

        await h.Sut.RegisterAsync(Register());

        h.Queue.Sent.Should().BeEmpty();
    }


    [Theory]
    [InlineData("http://waterbus.example")]
    [InlineData("https://waterbus.example?x=y")]
    [InlineData("https://waterbus.example#fragment")]
    public async Task Register_WithoutASafePublicBaseUrl_DoesNotQueue(string baseUrl)
    {
        var h = new Harness(baseUrl);
        await h.Sut.RegisterAsync(Register());
        h.Queue.Sent.Should().BeEmpty();
    }

    // ---------- Cooldown dùng chung giữa đăng ký và gửi lại ----------

    /// <summary>Cooldown thật (SET NX) trong bộ nhớ: chỉ lần chiếm đầu tiên cho tới khi hết hạn mới thành công.</summary>
    private static void UseStatefulCooldown(Harness h, Func<DateTimeOffset> now)
    {
        var expiry = new Dictionary<string, DateTimeOffset>();
        h.Cooldown.Setup(c => c.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns((string key, TimeSpan ttl, CancellationToken _) =>
            {
                lock (expiry)
                {
                    if (expiry.TryGetValue(key, out var until) && until > now()) return Task.FromResult(false);
                    expiry[key] = now() + ttl;
                    return Task.FromResult(true);
                }
            });
    }

    private static Harness RegisteredHarness(Func<DateTimeOffset> now, out ApplicationUser created)
    {
        var h = new Harness();
        UseStatefulCooldown(h, now);
        ApplicationUser? captured = null;
        h.Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>()))
            .Callback<ApplicationUser>(u => { u.Id = Guid.NewGuid(); captured = u; })
            .ReturnsAsync(IdentityResult.Success);
        h.Users.Setup(u => u.IsInRoleAsync(It.IsAny<ApplicationUser>(), "Passenger")).ReturnsAsync(true);
        h.Users.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync((ApplicationUser?)null);
        h.Sut.RegisterAsync(Register()).GetAwaiter().GetResult();
        created = captured!;
        h.Users.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(captured);
        return h;
    }

    [Fact]
    public async Task Register_ThenImmediateResend_ShouldSendOnlyOneEmail()
    {
        var clock = DateTimeOffset.UtcNow;
        var h = RegisteredHarness(() => clock, out _);

        await h.Sut.ResendVerificationAsync(new ResendVerificationRequest(Email));

        h.Queue.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Register_ThenResendAfterCooldown_ShouldSendSecondEmail()
    {
        var clock = DateTimeOffset.UtcNow;
        var h = RegisteredHarness(() => clock, out _);

        clock = clock.AddSeconds(61);
        await h.Sut.ResendVerificationAsync(new ResendVerificationRequest(Email));

        h.Queue.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task Register_ConcurrentResends_ShouldQueueAtMostOneAdditionalEmail()
    {
        var clock = DateTimeOffset.UtcNow.AddMinutes(-5);
        var h = RegisteredHarness(() => clock, out _);
        clock = clock.AddSeconds(61); // cooldown của đăng ký đã hết

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => h.Sut.ResendVerificationAsync(new ResendVerificationRequest(Email))));

        h.Queue.Sent.Should().HaveCount(2); // 1 đăng ký + đúng 1 gửi lại
    }

    [Fact]
    public async Task Register_WhenCooldownHeld_ShouldStillCreateUserButNotQueueEmail()
    {
        var h = new Harness();
        h.Cooldown.Setup(c => c.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var response = await h.Sut.RegisterAsync(Register());

        response.Should().NotBeNull();
        h.Users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>()), Times.Once);
        h.Queue.Sent.Should().BeEmpty();
    }

    // ---------- Gửi lại ----------

    private static async Task<Harness> ResendFor(ApplicationUser? user, bool passenger = true, bool cooldownFree = true)
    {
        var h = new Harness();
        h.Users.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(user);
        h.Users.Setup(u => u.IsInRoleAsync(It.IsAny<ApplicationUser>(), "Passenger")).ReturnsAsync(passenger);
        h.Cooldown.Setup(c => c.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(cooldownFree);
        await h.Sut.ResendVerificationAsync(new ResendVerificationRequest(Email));
        return h;
    }

    private static ApplicationUser Unverified() => new() { Id = Guid.NewGuid(), Email = Email, FullName = "A", EmailConfirmed = false, IsActive = true };

    [Fact]
    public async Task Resend_ForUnverifiedPassenger_ShouldQueueOneEmail() =>
        (await ResendFor(Unverified())).Queue.Sent.Should().ContainSingle();

    [Fact]
    public async Task Resend_ShouldSendNothingForUnknownVerifiedNonPassengerOrCoolingDownAccounts()
    {
        (await ResendFor(null)).Queue.Sent.Should().BeEmpty();
        (await ResendFor(new ApplicationUser { Email = Email, EmailConfirmed = true, IsActive = true })).Queue.Sent.Should().BeEmpty();
        (await ResendFor(Unverified(), passenger: false)).Queue.Sent.Should().BeEmpty();
        (await ResendFor(Unverified(), cooldownFree: false)).Queue.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Resend_ShouldRespondTheSameForKnownAndUnknownEmails()
    {
        var known = new Harness();
        known.Users.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(Unverified());
        known.Users.Setup(u => u.IsInRoleAsync(It.IsAny<ApplicationUser>(), "Passenger")).ReturnsAsync(true);

        var a = await known.Sut.ResendVerificationAsync(new ResendVerificationRequest(Email));
        var b = await new Harness().Sut.ResendVerificationAsync(new ResendVerificationRequest("nobody@gmail.com"));

        a.Should().Be(b);
    }

    [Fact]
    public async Task Register_ShouldTakeAboutTheSameTimeForNewAndExistingEmails()
    {
        var min = TimeSpan.FromMilliseconds(250);
        AccountService Make(bool existing)
        {
            var users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            users.Object.PasswordHasher = Mock.Of<IPasswordHasher<ApplicationUser>>();
            users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("tok");
            if (existing) users.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(new ApplicationUser { Email = Email });
            return new AccountService(users.Object, Mock.Of<IUnitOfWork>(), new CapturingQueue(), Mock.Of<IEmailCooldownStore>(),
                Microsoft.Extensions.Options.Options.Create(new EmailOptions { PublicBaseUrl = "https://x" }),
                NullLogger<AccountService>.Instance, min);
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        await Make(existing: false).RegisterAsync(Register());
        var fresh = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        started = System.Diagnostics.Stopwatch.GetTimestamp();
        await Make(existing: true).RegisterAsync(Register());
        var existingTime = System.Diagnostics.Stopwatch.GetElapsedTime(started);

        fresh.Should().BeGreaterThanOrEqualTo(min - TimeSpan.FromMilliseconds(15));
        existingTime.Should().BeGreaterThanOrEqualTo(min - TimeSpan.FromMilliseconds(15));
    }
}
