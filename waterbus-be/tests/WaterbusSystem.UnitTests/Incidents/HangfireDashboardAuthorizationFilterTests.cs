using System.Text;
using FluentAssertions;
using WaterbusSystem.WebApi.BackgroundJobs;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

public class HangfireDashboardAuthorizationFilterTests
{
    private static string Basic(string user, string pass) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));

    [Fact]
    public void CorrectCredentials_ShouldAuthorize() =>
        HangfireDashboardAuthorizationFilter.IsAuthorized(Basic("admin", "s3cret"), "admin", "s3cret", isDevelopment: false)
            .Should().BeTrue();

    [Theory]
    [InlineData("admin", "wrong")]
    [InlineData("other", "s3cret")]
    public void WrongCredentials_ShouldReject(string user, string pass) =>
        HangfireDashboardAuthorizationFilter.IsAuthorized(Basic(user, pass), "admin", "s3cret", isDevelopment: true)
            .Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer abc")]
    [InlineData("Basic not-base64!!")]
    public void MissingOrMalformedHeader_ShouldReject(string? header) =>
        HangfireDashboardAuthorizationFilter.IsAuthorized(header, "admin", "s3cret", isDevelopment: true)
            .Should().BeFalse();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void NoCredentialsConfigured_ShouldOnlyAllowDevelopment(bool isDevelopment, bool expected) =>
        HangfireDashboardAuthorizationFilter.IsAuthorized(null, null, null, isDevelopment).Should().Be(expected);
}
