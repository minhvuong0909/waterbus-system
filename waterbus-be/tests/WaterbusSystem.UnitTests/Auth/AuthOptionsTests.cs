using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.Features.Auth;
using Xunit;

namespace WaterbusSystem.UnitTests.Auth;

/// <summary>Cấu hình role theo cổng đăng nhập: mặc định, ghi đè từ cấu hình và kiểm tra khi khởi động.</summary>
public class AuthOptionsTests
{
    private static readonly AuthOptionsValidator Validator = new();

    [Fact]
    public void RolesFor_WithoutConfiguration_ShouldUseDefaults()
    {
        var options = new AuthOptions();

        options.RolesFor(AuthPortal.Web).Should().BeEquivalentTo("Admin", "Captain");
        options.RolesFor(AuthPortal.Scanner).Should().BeEquivalentTo("Staff");
    }

    [Fact]
    public void Bind_WithConfiguredPortal_ShouldReplaceDefaultWithoutDuplicates()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Portals:Web:0"] = "Admin",
        }).Build();

        var options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()!;

        options.RolesFor(AuthPortal.Web).Should().Equal("Admin");
        options.RolesFor(AuthPortal.Scanner).Should().BeEquivalentTo("Staff");
    }

    [Fact]
    public void Validate_WithDefaultsAndValidOverrides_ShouldSucceed()
    {
        Validator.Validate(null, new AuthOptions()).Succeeded.Should().BeTrue();
        Validator.Validate(null, new AuthOptions { Portals = { ["web"] = ["Admin", "captain"] } }).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyRoleList_ShouldFail()
    {
        var result = Validator.Validate(null, new AuthOptions { Portals = { ["Web"] = [] } });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Web");
    }

    [Theory]
    [InlineData("Mobile", "Admin")]
    [InlineData("Web", "Dispatcher")]
    public void Validate_WithUnknownPortalOrRole_ShouldFail(string portal, string role)
    {
        Validator.Validate(null, new AuthOptions { Portals = { [portal] = [role] } }).Failed.Should().BeTrue();
    }
}
