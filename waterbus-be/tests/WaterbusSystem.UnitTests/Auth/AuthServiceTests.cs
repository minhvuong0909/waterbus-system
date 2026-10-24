using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
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
            .Setup(m => m.CheckPasswordSignInAsync(user, "Secret123", false))
            .ReturnsAsync(SignInResult.Success);

        var jwtGeneratorMock = new Mock<IJwtTokenGenerator>();
        jwtGeneratorMock
            .Setup(j => j.GenerateToken(user.Id, user.Email!, user.FullName, It.IsAny<IList<string>>()))
            .Returns("fake-jwt-token");

        var sut = new AuthService(userManagerMock.Object, signInManagerMock.Object, jwtGeneratorMock.Object);

        var response = await sut.LoginAsync(new LoginRequest(user.Email!, "Secret123"));

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
            .Setup(m => m.CheckPasswordSignInAsync(user, "WrongPassword", false))
            .ReturnsAsync(SignInResult.Failed);

        var sut = new AuthService(userManagerMock.Object, signInManagerMock.Object, Mock.Of<IJwtTokenGenerator>());

        var act = () => sut.LoginAsync(new LoginRequest(user.Email!, "WrongPassword"));

        await act.Should().ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task LoginAsync_WhenAccountIsInactive_ShouldThrowAuthenticationException()
    {
        var user = ActiveUser("staff@waterbus.vn");
        user.IsActive = false;
        var userManagerMock = CreateUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);

        var signInManagerMock = CreateSignInManagerMock(userManagerMock.Object);

        var sut = new AuthService(userManagerMock.Object, signInManagerMock.Object, Mock.Of<IJwtTokenGenerator>());

        var act = () => sut.LoginAsync(new LoginRequest(user.Email!, "Secret123"));

        await act.Should().ThrowAsync<AuthenticationException>();
        signInManagerMock.Verify(
            m => m.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }
}
