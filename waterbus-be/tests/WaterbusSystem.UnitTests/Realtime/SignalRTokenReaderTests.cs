using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using WaterbusSystem.Infrastructure.Identity;
using Xunit;

namespace WaterbusSystem.UnitTests.Realtime;

/// <summary>
/// SignalR truyền JWT qua query access_token; chỉ được nhận token kiểu này cho đường dẫn /hubs.
/// </summary>
public class SignalRTokenReaderTests
{
    private static MessageReceivedContext CreateContext(string path, string? accessToken)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        if (accessToken is not null)
        {
            httpContext.Request.QueryString = QueryString.Create("access_token", accessToken);
        }

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        return new MessageReceivedContext(httpContext, scheme, new JwtBearerOptions());
    }

    [Theory]
    [InlineData("/hubs/boat-tracking")]
    [InlineData("/hubs/seat-sync/negotiate")]
    public async Task OnMessageReceived_HubPathWithQueryToken_ShouldUseQueryToken(string path)
    {
        var context = CreateContext(path, "jwt-token");

        await SignalRTokenReader.OnMessageReceived(context);

        context.Token.Should().Be("jwt-token");
    }

    [Fact]
    public async Task OnMessageReceived_RestPathWithQueryToken_ShouldIgnoreQueryToken()
    {
        var context = CreateContext("/api/v1/stations", "jwt-token");

        await SignalRTokenReader.OnMessageReceived(context);

        context.Token.Should().BeNull();
    }

    [Theory]
    [InlineData("/hubs/boat-tracking", null)]
    [InlineData("/hubs/boat-tracking", "")]
    [InlineData("/hubsfake/boat-tracking", "jwt-token")] // chỉ khớp đúng segment /hubs
    public async Task OnMessageReceived_MissingTokenOrNotHubSegment_ShouldLeaveTokenUnset(string path, string? token)
    {
        var context = CreateContext(path, token);

        await SignalRTokenReader.OnMessageReceived(context);

        context.Token.Should().BeNull();
    }
}
