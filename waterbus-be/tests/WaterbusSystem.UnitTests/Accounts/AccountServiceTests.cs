using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Infrastructure.Identity;
using Xunit;

namespace WaterbusSystem.UnitTests.Accounts;

/// <summary>Đăng ký khách hàng, xác minh email, gửi lại: đúng role, không lộ email tồn tại, không gửi spam.</summary>
public class AccountServiceTests
{
    private const string Email = "customer@gmail.com";
    private const string Password = "Strong1Pass";

    private sealed class CapturingQueue : IVerificationEmailQueue
    {
        public readonly List<(string Email, string Name, string Link)> Sent = new();
        public void Enqueue(string toEmail, string toName, string verificationLink) => Sent.Add((toEmail, toName, verificationLink));
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
            Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
            Users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Passenger")).ReturnsAsync(IdentityResult.Success);
            Users.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("raw+token/with=chars");
            Cooldown.Setup(c => c.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Sut = new AccountService(Users.Object, Queue, Cooldown.Object,
                Microsoft.Extensions.Options.Options.Create(new EmailOptions { PublicBaseUrl = publicBaseUrl }),
                NullLogger<AccountService>.Instance, minResponseTime: TimeSpan.Zero);
        }
    }

    private static RegisterRequest Register(string email = Email, string password = Password) =>
        new(email, password, "Nguyen Van A", "0901234567");

    // ---------- Đăng ký ----------

    [Fact]
    public async Task Register_NewEmail_ShouldCreateUnverifiedPassengerAndQueueVerificationEmail()
    {
        var h = new Harness();

        await h.Sut.RegisterAsync(Register());

        h.Users.Verify(u => u.CreateAsync(It.Is<ApplicationUser>(x => x.Email == Email && x.EmailConfirmed == false && x.IsActive), Password), Times.Once);
        h.Users.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Passenger"), Times.Once);
        var sent = h.Queue.Sent.Should().ContainSingle().Subject;
        sent.Email.Should().Be(Email);
        sent.Link.Should().StartWith("https://waterbus.example/verify-email?uid=").And.Contain("&token=");
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
        h.Users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        h.Queue.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WeakPassword_ShouldFailBeforeLookingUpTheEmail()
    {
        var h = new Harness();
        var validator = new Mock<IPasswordValidator<ApplicationUser>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<UserManager<ApplicationUser>>(), It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "Mật khẩu quá ngắn." }));
        h.Users.Object.PasswordValidators.Add(validator.Object);

        var act = () => h.Sut.RegisterAsync(Register(password: "weak"));

        await act.Should().ThrowAsync<ValidationException>();
        // Không dò được email nào đã tồn tại qua lỗi mật khẩu yếu.
        h.Users.Verify(u => u.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenAnotherRequestWonTheRace_ShouldTreatItAsExistingEmail()
    {
        var h = new Harness();
        h.Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
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

    // ---------- Xác minh email ----------

    private static ConfirmEmailRequest Confirm(Guid id, string rawToken = "raw+token/with=chars") =>
        new(id, WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken)));

    [Fact]
    public async Task ConfirmEmail_WithValidToken_ShouldPassTheDecodedTokenToIdentity()
    {
        var h = new Harness();
        var user = new ApplicationUser { Id = Guid.NewGuid(), IsActive = true };
        h.Users.Setup(u => u.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        h.Users.Setup(u => u.ConfirmEmailAsync(user, "raw+token/with=chars")).ReturnsAsync(IdentityResult.Success);

        var act = () => h.Sut.ConfirmEmailAsync(Confirm(user.Id));

        await act.Should().NotThrowAsync();
        h.Users.Verify(u => u.ConfirmEmailAsync(user, "raw+token/with=chars"), Times.Once);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidTokenUnknownUserOrBrokenEncoding_ShouldAllThrowTheSameError()
    {
        var h = new Harness();
        var user = new ApplicationUser { Id = Guid.NewGuid(), IsActive = true };
        h.Users.Setup(u => u.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        h.Users.Setup(u => u.ConfirmEmailAsync(user, It.IsAny<string>())).ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        var wrongToken = (await ((Func<Task>)(() => h.Sut.ConfirmEmailAsync(Confirm(user.Id)))).Should().ThrowAsync<ValidationException>()).Which.Errors;
        var unknownUser = (await ((Func<Task>)(() => h.Sut.ConfirmEmailAsync(Confirm(Guid.NewGuid())))).Should().ThrowAsync<ValidationException>()).Which.Errors;
        var brokenEncoding = (await ((Func<Task>)(() => h.Sut.ConfirmEmailAsync(new ConfirmEmailRequest(user.Id, "***not base64***")))).Should().ThrowAsync<ValidationException>()).Which.Errors;

        unknownUser.Should().BeEquivalentTo(wrongToken);
        brokenEncoding.Should().BeEquivalentTo(wrongToken);
    }

    [Fact]
    public async Task ConfirmEmail_ForInactiveAccount_ShouldBeRejected()
    {
        var h = new Harness();
        var user = new ApplicationUser { Id = Guid.NewGuid(), IsActive = false };
        h.Users.Setup(u => u.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);

        var act = () => h.Sut.ConfirmEmailAsync(Confirm(user.Id));

        await act.Should().ThrowAsync<ValidationException>();
        h.Users.Verify(u => u.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
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
            users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("tok");
            if (existing) users.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(new ApplicationUser { Email = Email });
            return new AccountService(users.Object, new CapturingQueue(), Mock.Of<IEmailCooldownStore>(),
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
