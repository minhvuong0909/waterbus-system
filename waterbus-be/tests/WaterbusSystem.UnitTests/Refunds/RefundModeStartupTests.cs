using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WaterbusSystem.Application;
using WaterbusSystem.Application.Features.Refunds;
using WaterbusSystem.Infrastructure;
using Xunit;

namespace WaterbusSystem.UnitTests.Refunds;

/// <summary>
/// Khởi động THẬT (WebApplication + đăng ký production AddInfrastructureServices): cấu hình Refunds:Mode sai phải làm host
/// KHÔNG khởi động được, trước mọi thao tác hoàn tiền. Bỏ mọi hosted service nghiệp vụ để không chạm DB/Redis/Hangfire; kiểm tra options (ValidateOnStart) vẫn chạy khi StartAsync.
/// </summary>
public sealed class RefundModeStartupTests
{
    private static WebApplication Build(string? mode, string? baseUrl = null, string? webApiUrl = null)
    {
        using var key = RSA.Create(2048);
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Database=NotUsed;Integrated Security=True",
            ["JwtSettings:Secret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["VnPay:TmnCode"] = "STARTUP01",
            ["VnPay:HashSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Qr:PrivateKeyPem"] = key.ExportPkcs8PrivateKeyPem()
        };
        if (mode is not null) values["Refunds:Mode"] = mode;
        if (baseUrl is not null) values["VnPay:BaseUrl"] = baseUrl;
        if (webApiUrl is not null) values["VnPay:WebApiUrl"] = webApiUrl;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        // .NET 8: ValidateOnStart đăng ký IStartupValidator; host gọi nó khi StartAsync (trước mọi hosted service khác).
        Assert.Contains(builder.Services, d => d.ServiceType.Name == "IStartupValidator");
        var drop = builder.Services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
        foreach (var d in drop) builder.Services.Remove(d);
        return builder.Build();
    }

    /// <summary>Chỉ trả lỗi của StartAsync (khởi động). Lỗi khi dừng host KHÔNG được che thành "đã từ chối khởi động".</summary>
    private static async Task<Exception?> StartAsync(string? mode, string? baseUrl = null, string? webApiUrl = null)
    {
        await using var app = Build(mode, baseUrl, webApiUrl);
        Exception? startupError = null;
        try { await app.StartAsync(); }
        catch (Exception e) { startupError = e; }
        if (startupError is null) await app.StopAsync();
        return startupError;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Manual")]
    [InlineData("Gateway")]
    public async Task ValidModes_StartTheRealHost(string? mode) => Assert.Null(await StartAsync(mode));

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("-1")]
    [InlineData("Banana")]
    [InlineData("Demo")]   // chế độ Demo đã bị gỡ: cấu hình cũ phải làm host không khởi động, không âm thầm đổi hành vi
    [InlineData("demo-ish")]
    public async Task InvalidMode_PreventsHostStartup(string mode)
    {
        var error = await StartAsync(mode);
        Assert.NotNull(error);
        // Lỗi phải đến từ kiểm tra options (OptionsValidationException) hoặc bind enum, và nêu đúng cấu hình sai.
        Assert.True(error is Microsoft.Extensions.Options.OptionsValidationException ||
            error.ToString().Contains("Refunds:Mode", StringComparison.Ordinal) || error.ToString().Contains("RefundOptions", StringComparison.Ordinal),
            error.ToString());
        Assert.DoesNotContain("StopAsync", error.ToString());
    }

    [Theory]
    [InlineData("https://pay.vnpay.vn/vpcpay.html", null)]
    [InlineData(null, "https://merchant.vnpayment.vn/merchant_webapi/api/transaction")]
    [InlineData("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction")]
    [InlineData(null, null)]
    public async Task ManualMode_StartsWithAnyVnPayEndpoint_BecauseItIsTheRealProcessAndNeverCallsRefund(string? baseUrl, string? webApiUrl) =>
        Assert.Null(await StartAsync("Manual", baseUrl, webApiUrl));
}
