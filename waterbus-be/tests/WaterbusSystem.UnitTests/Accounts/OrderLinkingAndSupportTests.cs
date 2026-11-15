using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Accounts;
using WaterbusSystem.Application.Features.Accounts;
using WaterbusSystem.Application.Features.Accounts.Services;
using WaterbusSystem.Application.Features.Notifications.Templates;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Validators.Accounts;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.Accounts;

public class OrderLinkingAndSupportTests
{
    // ---------- Gắn đơn theo email đã xác minh ----------

    private sealed class Harness
    {
        public readonly Mock<IUserDirectory> Users = new();
        public readonly Mock<IOrderLinkingRepository> Repo = new();
        public readonly Mock<IUnitOfWork> Uow = new();
        public readonly Guid AccountId = Guid.NewGuid();
        public readonly OrderLinkingService Sut;

        public Harness(string? verifiedEmail = "buyer@gmail.com")
        {
            Users.Setup(u => u.GetVerifiedEmailAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(verifiedEmail);
            Sut = new OrderLinkingService(Users.Object, Repo.Object, new InlineRunner(), Uow.Object, NullLogger<OrderLinkingService>.Instance);
        }

        private sealed class InlineRunner : ICommercialTransactionRunner
        {
            public Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
        }
    }

    private static List<PurchaseOrder> Orders(int n) => Enumerable.Range(0, n).Select(_ => new PurchaseOrder { Id = Guid.NewGuid() }).ToList();

    [Fact]
    public async Task Link_WithVerifiedEmail_ShouldAssignTheAccountToEveryFoundOrderAndSave()
    {
        var h = new Harness();
        var orders = Orders(3);
        h.Repo.Setup(r => r.GetLinkableTrackedAsync("buyer@gmail.com", OrderLinkingService.MaxPerCall, It.IsAny<CancellationToken>())).ReturnsAsync(orders);

        var result = await h.Sut.LinkByVerifiedEmailAsync(h.AccountId);

        orders.Should().OnlyContain(o => o.PassengerAccountId == h.AccountId);
        result.Should().Be(new WaterbusSystem.Application.DTOs.Responses.Accounts.LinkOrdersResponse(3, 0));
        h.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Link_WhenEmailIsNotVerified_ShouldBeForbiddenAndTouchNoOrders()
    {
        var h = new Harness(verifiedEmail: null);

        var link = () => h.Sut.LinkByVerifiedEmailAsync(h.AccountId);
        var count = () => h.Sut.CountLinkableAsync(h.AccountId);

        await link.Should().ThrowAsync<ForbiddenAccessException>();
        await count.Should().ThrowAsync<ForbiddenAccessException>();
        h.Repo.Verify(r => r.GetLinkableTrackedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Link_WhenNothingToLink_ShouldNotSave()
    {
        var h = new Harness();
        h.Repo.Setup(r => r.GetLinkableTrackedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<PurchaseOrder>());

        var result = await h.Sut.LinkByVerifiedEmailAsync(h.AccountId);

        result.Linked.Should().Be(0);
        h.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Link_WhenBatchIsFull_ShouldReportHowManyRemain()
    {
        var h = new Harness();
        h.Repo.Setup(r => r.GetLinkableTrackedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Orders(OrderLinkingService.MaxPerCall));
        h.Repo.Setup(r => r.CountLinkableAsync("buyer@gmail.com", It.IsAny<CancellationToken>())).ReturnsAsync(37);

        var result = await h.Sut.LinkByVerifiedEmailAsync(h.AccountId);

        result.Linked.Should().Be(OrderLinkingService.MaxPerCall);
        result.Remaining.Should().Be(37);
    }

    // ---------- Validator ----------

    private readonly RegisterRequestValidator _register = new();

    [Theory]
    [InlineData("a@b.vn", "Strong1Pass", "Nguyen A", null, true)]
    [InlineData("a@b.vn", "Strong1Pass", "Nguyen A", "0901 234 567", true)]
    [InlineData("", "Strong1Pass", "Nguyen A", null, false)]
    [InlineData("not-an-email", "Strong1Pass", "Nguyen A", null, false)]
    [InlineData("a@b.vn", "short1A", "Nguyen A", null, false)]       // dưới 8 ký tự
    [InlineData("a@b.vn", "alllowercase1", "Nguyen A", null, false)] // thiếu chữ hoa
    [InlineData("a@b.vn", "ALLUPPERCASE1", "Nguyen A", null, false)] // thiếu chữ thường
    [InlineData("a@b.vn", "NoDigitsHere", "Nguyen A", null, false)]  // thiếu chữ số
    [InlineData("a@b.vn", "Strong1Pass", "", null, false)]
    [InlineData("a@b.vn", "Strong1Pass", "Nguyen A", "abc", false)]
    public void RegisterValidator_AcceptsOnlyWellFormedInput(string email, string password, string name, string? phone, bool valid) =>
        _register.Validate(new RegisterRequest(email, password, name, phone)).IsValid.Should().Be(valid);

    [Fact]
    public void RegisterValidator_RejectsEmailLongerThanTheOrderEmailColumn()
    {
        // PurchaserEmail dài tối đa 150: email tài khoản dài hơn thì không bao giờ khớp đơn để gắn.
        var email = new string('a', 142) + "@gmail.com";

        _register.Validate(new RegisterRequest(email, "Strong1Pass", "A", null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ConfirmEmailValidator_RequiresIdAndToken()
    {
        var v = new ConfirmEmailRequestValidator();

        v.Validate(new ConfirmEmailRequest(Guid.NewGuid(), "abc")).IsValid.Should().BeTrue();
        v.Validate(new ConfirmEmailRequest(Guid.Empty, "abc")).IsValid.Should().BeFalse();
        v.Validate(new ConfirmEmailRequest(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        v.Validate(new ConfirmEmailRequest(Guid.NewGuid(), new string('a', 1025))).IsValid.Should().BeFalse();
    }

    // ---------- Email ----------

    [Fact]
    public void VerificationEmail_ContainsTheLinkAndNoOrderData_AndEncodesTheName()
    {
        var ctx = new EmailContext("Smart Waterbus", "https://waterbus.example", null, null);

        var email = AccountEmailTemplates.Verification("<script>x</script>", "https://waterbus.example/verify-email?uid=1&token=abc", ctx);

        email.Text.Should().Contain("verify-email?uid=1&token=abc");
        email.Html.Should().NotContain("<script>");
        email.Html.Should().Contain("24 giờ");
    }

    // ---------- Controller ----------

    [Theory]
    [InlineData(nameof(AuthController.Register))]
    [InlineData(nameof(AuthController.ConfirmEmail))]
    [InlineData(nameof(AuthController.ResendVerification))]
    public void PublicAccountActions_AreAnonymousAndRateLimited(string action)
    {
        var method = typeof(AuthController).GetMethod(action)!;

        method.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be("AuthPolicy");
    }

    [Fact]
    public void MeController_IsPassengerOnly()
    {
        typeof(MeController).GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("Passenger");
    }
}
