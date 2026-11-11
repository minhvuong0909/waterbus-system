using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.Auth;

/// <summary>
/// M11: endpoint Login phải gắn policy rate limit riêng (chặn brute-force), vì Login luôn là
/// anonymous request nên không thể dựa vào BookingPolicy (partition theo User đã đăng nhập).
/// </summary>
public class AuthControllerRateLimitingTests
{
    [Fact]
    public void Login_ShouldBeDecoratedWithAuthPolicyRateLimiting()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Login));

        var attribute = method!.GetCustomAttribute<EnableRateLimitingAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PolicyName.Should().Be("AuthPolicy");
    }
}
