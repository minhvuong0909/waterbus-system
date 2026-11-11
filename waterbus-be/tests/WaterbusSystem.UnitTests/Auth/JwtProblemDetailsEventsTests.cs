using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using WaterbusSystem.Infrastructure.Identity;
using Xunit;

namespace WaterbusSystem.UnitTests.Auth;

/// <summary>
/// M10: 401 (Challenge) và 403 (Forbidden) của JWT phải trả cùng hình dạng ProblemDetails + traceId
/// mà GlobalExceptionHandlingMiddleware dùng cho các lỗi khác, không phải response mặc định của Identity.
/// </summary>
public class JwtProblemDetailsEventsTests
{
    private static readonly AuthenticationScheme Scheme = new("Bearer", "Bearer", typeof(JwtBearerHandler));

    private static DefaultHttpContext NewContext(string path, string traceId)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.TraceIdentifier = traceId;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadBody(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        var json = await reader.ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task OnChallenge_ShouldWrite401ProblemDetailsWithTraceId()
    {
        var httpContext = NewContext("/api/v1/boats", "trace-401");
        var challengeContext = new JwtBearerChallengeContext(
            httpContext, Scheme, new JwtBearerOptions(), new AuthenticationProperties());

        await JwtProblemDetailsEvents.OnChallenge(challengeContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        httpContext.Response.ContentType.Should().Be("application/problem+json");

        var body = await ReadBody(httpContext.Response);
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status401Unauthorized);
        body.GetProperty("instance").GetString().Should().Be("/api/v1/boats");
        body.GetProperty("traceId").GetString().Should().Be("trace-401");
    }

    [Fact]
    public async Task OnForbidden_ShouldWrite403ProblemDetailsWithTraceId()
    {
        var httpContext = NewContext("/api/v1/boats", "trace-403");
        var forbiddenContext = new ForbiddenContext(httpContext, Scheme, new JwtBearerOptions());

        await JwtProblemDetailsEvents.OnForbidden(forbiddenContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        httpContext.Response.ContentType.Should().Be("application/problem+json");

        var body = await ReadBody(httpContext.Response);
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status403Forbidden);
        body.GetProperty("instance").GetString().Should().Be("/api/v1/boats");
        body.GetProperty("traceId").GetString().Should().Be("trace-403");
    }
}
