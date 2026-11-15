using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Features.Auth;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Auth;
using WaterbusSystem.Infrastructure.Identity;
using Xunit;

namespace WaterbusSystem.UnitTests.Auth;

/// <summary>
/// Kiểm thử AuthService: đăng nhập đúng, sai mật khẩu, tài khoản bị khoá
/// </summary>
public class AuthServiceTests
{
    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManagerMock(UserManager<ApplicationUser> userManager)
    {
        return new Mock<SignInManager<ApplicationUser>>(
            userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!);
    }

    private static ApplicationUser ActiveUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        FullName = "Nguyen Van Staff",
        IsActive = true
    };

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreCorrect_ShouldReturnLoginResponseWithToken()
    {
        var user = ActiveUser("staff@waterbus.vn");
        var userManagerMock = CreateUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Staff" });

        var signInManagerMock = CreateSignInManagerMock(userManagerMock.Object);
        signInManagerMock
            .Setup(m => m.CheckPasswordSignInAsync(user, "Secret123", true))
            .ReturnsAsync(SignInResult.Success);

        var jwtGeneratorMock = new Mock<IJwtTokenGenerator>();
        jwtGeneratorMock
            .Setup(j => j.GenerateToken(user.Id, user.Email!, user.FullName, It.IsAny<IList<string>>()))
            .Returns("fake-jwt-token");

        var sut = new AuthService(userManagerMock.Object, signInManagerMock.Object, jwtGeneratorMock.Object, Options.Create(new AuthOptions()));

        var response = await sut.LoginAsync(new LoginRequest(user.Email!, "Secret123"), AuthPortal.Scanner);

        response.Token.Should().Be("fake-jwt-token");
        response.UserId.Should().Be(user.Id);
        response.Roles.Should().Contain("Staff");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsWrong_ShouldThrowAuthenticationException()
    {
        var user = ActiveUser("staff@waterbus.vn");
        var userManagerMock = CreateUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);

        var signInManagerMock = CreateSignInManagerMock(userManagerMock.Object);
        signInManagerMock
            .Setup(m => m.CheckPasswordSignInAsync(user, "WrongPassword", true))
            .ReturnsAsync(SignInResult.Failed);

        var sut = new AuthService(userManagerMock.Object, signInManagerMock.Object, Mock.Of<IJwtTokenGenerator>(), Options.Create(new AuthOptions()));

        var act = () => sut.LoginAsync(new LoginRequest(user.Email!, "WrongPassword"), AuthPortal.Web);

        await act.Should().ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task LoginAsync_WhenAccountIsLockedOut_ShouldThrowAuthenticationExceptionWithLockoutMessage()
    {
        var user = ActiveUser("staff@waterbus.vn");
        var userManagerMock = CreateUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);

        var signInManagerMock = CreateSignInManagerMock(userManagerMock.Object);
        signInManagerMock
            .Setup(m => m.CheckPasswordSignInAsync(user, "WrongPassword", true))
            .ReturnsAsync(SignInResult.LockedOut);

        var sut = new AuthService(userManagerMock.Object, signInManagerMock.Object, Mock.Of<IJwtTokenGenerator>(), Options.Create(new AuthOptions()));

        var act = () => sut.LoginAsync(new LoginRequest(user.Email!, "WrongPassword"), AuthPortal.Web);

        (await act.Should().ThrowAsync<AuthenticationException>())
            .WithMessage("*tạm khóa*");
    }

    [Fact]
    public async Task LoginAsync_WhenAccountIsInactive_ShouldThrowAuthenticationException()
    {
        var user = ActiveUser("staff@waterbus.vn");
        user.IsActive = false;
        var userManagerMock = CreateUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);

        var signInManagerMock = CreateSignInManagerMock(userManagerMock.Object);

        var sut = new AuthService(userManagerMock.Object, signInManagerMock.Object, Mock.Of<IJwtTokenGenerator>(), Options.Create(new AuthOptions()));

        var act = () => sut.LoginAsync(new LoginRequest(user.Email!, "Secret123"), AuthPortal.Web);

        await act.Should().ThrowAsync<AuthenticationException>();
        signInManagerMock.Verify(
            m => m.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    private static AuthService CreatePortalSut(string role, AuthOptions? options = null)
    {
        var user = ActiveUser("who@waterbus.vn");
        var userManagerMock = CreateUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { role });
        var signInManagerMock = CreateSignInManagerMock(userManagerMock.Object);
        signInManagerMock.Setup(m => m.CheckPasswordSignInAsync(user, "Secret123", true)).ReturnsAsync(SignInResult.Success);
        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>())).Returns("jwt");
        return new AuthService(userManagerMock.Object, signInManagerMock.Object, jwt.Object, Options.Create(options ?? new AuthOptions()));
    }

    [Theory]
    [InlineData("Admin", AuthPortal.Web)]
    [InlineData("Captain", AuthPortal.Web)]
    [InlineData("Staff", AuthPortal.Scanner)]
    public async Task LoginAsync_WhenRoleBelongsToPortal_ShouldSucceed(string role, AuthPortal portal)
    {
        var response = await CreatePortalSut(role).LoginAsync(new LoginRequest("who@waterbus.vn", "Secret123"), portal);

        response.Token.Should().Be("jwt");
    }

    [Theory]
    [InlineData("Passenger", AuthPortal.Web)]
    [InlineData("Staff", AuthPortal.Web)]
    [InlineData("Admin", AuthPortal.Scanner)]
    [InlineData("Captain", AuthPortal.Scanner)]
    public async Task LoginAsync_WhenRoleIsNotInPortal_ShouldRejectWithSameMessageAsWrongPassword(string role, AuthPortal portal)
    {
        var act = () => CreatePortalSut(role).LoginAsync(new LoginRequest("who@waterbus.vn", "Secret123"), portal);

        (await act.Should().ThrowAsync<AuthenticationException>()).WithMessage("Email hoặc mật khẩu không chính xác.");
    }

    [Fact]
    public async Task LoginAsync_WhenUserHasAllowedRoleAmongSeveral_ShouldSucceed()
    {
        var user = ActiveUser("who@waterbus.vn");
        var userManagerMock = CreateUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Staff", "Admin" });
        var signInManagerMock = CreateSignInManagerMock(userManagerMock.Object);
        signInManagerMock.Setup(m => m.CheckPasswordSignInAsync(user, "Secret123", true)).ReturnsAsync(SignInResult.Success);
        var sut = new AuthService(userManagerMock.Object, signInManagerMock.Object, Mock.Of<IJwtTokenGenerator>(), Options.Create(new AuthOptions()));

        var act = () => sut.LoginAsync(new LoginRequest(user.Email!, "Secret123"), AuthPortal.Web);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LoginAsync_WhenConfigurationOverridesPortal_ShouldFollowConfiguration()
    {
        var options = new AuthOptions { Portals = { ["Web"] = ["Passenger"] } };

        var passenger = await CreatePortalSut("Passenger", options).LoginAsync(new LoginRequest("who@waterbus.vn", "Secret123"), AuthPortal.Web);
        var admin = () => CreatePortalSut("Admin", options).LoginAsync(new LoginRequest("who@waterbus.vn", "Secret123"), AuthPortal.Web);

        passenger.Token.Should().Be("jwt");
        await admin.Should().ThrowAsync<AuthenticationException>();
    }
}
