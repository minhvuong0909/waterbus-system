using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.DTOs.Requests.Tickets;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Features.Notifications.Templates;
using WaterbusSystem.Application.Features.Tickets;
using WaterbusSystem.Application.Validators.Tickets;
using WaterbusSystem.Infrastructure.Services.TicketLookup;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.Tickets;

public class TicketLookupSupportTests
{
    private static HmacTicketLookupHasher Hasher(string secret = "0123456789012345678901234567890123456789") =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:Secret"] = secret }).Build());

    // ---------- Hasher ----------

    [Fact]
    public void Hasher_IsDeterministicAndSeparatedByPurposeAndSecret()
    {
        var a = Hasher();

        a.Hash("p", "v").Should().Be(a.Hash("p", "v"));
        a.Hash("p", "v").Should().NotBe(a.Hash("q", "v"));
        a.Hash("p", "v").Should().NotBe(Hasher("another-secret-another-secret-another!!").Hash("p", "v"));
        a.Hash("p", "v").Should().HaveLength(64).And.MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void Hasher_Equals_ComparesValues()
    {
        var a = Hasher();

        a.Equals(a.Hash("p", "1"), a.Hash("p", "1")).Should().BeTrue();
        a.Equals(a.Hash("p", "1"), a.Hash("p", "2")).Should().BeFalse();
    }

    // ---------- Validator ----------

    private readonly RequestTicketLookupOtpRequestValidator _request = new();
    private readonly VerifyTicketLookupOtpRequestValidator _verify = new();

    [Theory]
    [InlineData("a@b.vn", "WB20261114101530ABCDEF", true)]
    [InlineData("a@b.vn", "wb20261114101530abcdef", true)]
    [InlineData("a@b.vn", " WB20261114101530ABCDEF ", true)]
    [InlineData("", "WB20261114101530ABCDEF", false)]
    [InlineData("not-an-email", "WB20261114101530ABCDEF", false)]
    [InlineData("a@b.vn", "", false)]
    [InlineData("a@b.vn", "WB2026111410153OABCDEF", false)] // chứa O, không thuộc bảng chữ của CodeGenerator
    [InlineData("a@b.vn", "TK20261114101530ABCDEF", false)] // mã vé, không phải mã đặt chỗ
    public void RequestValidator_AcceptsOnlyWellFormedInput(string email, string code, bool valid) =>
        _request.Validate(new RequestTicketLookupOtpRequest(email, code)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("123456", true)]
    [InlineData("000000", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("12345a", false)]
    [InlineData("", false)]
    public void VerifyValidator_RequiresSixDigits(string otp, bool valid) =>
        _verify.Validate(new VerifyTicketLookupOtpRequest("a@b.vn", "WB20261114101530ABCDEF", otp)).IsValid.Should().Be(valid);

    // ---------- Options ----------

    [Fact]
    public void Options_DefaultsAreValid_AndOutOfRangeValuesAreRejected()
    {
        var validator = new TicketLookupOptionsValidator();

        validator.Validate(null, new TicketLookupOptions()).Succeeded.Should().BeTrue();
        validator.Validate(null, new TicketLookupOptions { OtpTtlMinutes = 0 }).Failed.Should().BeTrue();
        validator.Validate(null, new TicketLookupOptions { MaxVerifyAttempts = 100 }).Failed.Should().BeTrue();
        validator.Validate(null, new TicketLookupOptions { ResendCooldownSeconds = 1 }).Failed.Should().BeTrue();
        validator.Validate(null, new TicketLookupOptions { MaxSendsPerHour = 0 }).Failed.Should().BeTrue();
    }

    // ---------- Email ----------

    [Fact]
    public void EmailTemplate_ContainsOtpAndValidity_ButNoBookingDataAndEncodesName()
    {
        var ctx = new EmailContext("Smart Waterbus", null, null, null);

        var email = TicketLookupEmailTemplate.Render("<script>alert(1)</script>", "482913", TimeSpan.FromMinutes(5), ctx);

        email.Text.Should().Contain("482913").And.Contain("5 phút");
        email.Html.Should().Contain("482913").And.NotContain("<script>");
        email.Html.Should().NotContain("WB2026");
    }

    // ---------- Controller ----------

    [Theory]
    [InlineData(nameof(TicketLookupController.RequestOtp))]
    [InlineData(nameof(TicketLookupController.Verify))]
    public void Actions_AreAnonymousAndRateLimited(string action)
    {
        var method = typeof(TicketLookupController).GetMethod(action)!;

        method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be("TicketLookupPolicy");
        typeof(TicketLookupController).GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
    }
}
