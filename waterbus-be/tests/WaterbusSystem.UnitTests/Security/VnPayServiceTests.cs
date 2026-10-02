using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Infrastructure.Services.Payment;
using Xunit;

namespace WaterbusSystem.UnitTests.Security;

/// <summary>
/// Test thật cho VnPayService (không chỉ test FixedTimeEquals suông như trước đây):
/// round-trip tạo URL thanh toán rồi tự xác thực lại chữ ký, phát hiện tampering, và kiểm tra TmnCode.
/// </summary>
public class VnPayServiceTests
{
    private const string TmnCode = "TESTCODE01";
    private const string HashSecret = "TEST_SECRET_KEY_FOR_UNIT_TEST_ONLY";

    private static VnPayService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VnPay:TmnCode"] = TmnCode,
                ["VnPay:HashSecret"] = HashSecret,
                ["VnPay:BaseUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                ["VnPay:ReturnUrl"] = "http://localhost:5173/payment/callback"
            })
            .Build();

        return new VnPayService(configuration);
    }

    private static Dictionary<string, string> ParseQuery(string url)
    {
        // Dùng WebUtility.UrlDecode (không phải Uri.UnescapeDataString) để khớp với cách
        // ASP.NET Core Request.Query thực sự giải mã '+' thành khoảng trắng, đồng bộ với
        // WebUtility.UrlEncode mà VnPayService dùng để tạo URL.
        var query = url.Split('?', 2)[1];
        return query.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => System.Net.WebUtility.UrlDecode(p[1]));
    }

    [Fact]
    public void CreatePaymentUrl_ThenValidateSignature_ShouldReturnTrue()
    {
        var service = CreateService();

        var url = service.CreatePaymentUrl("WB20261002ABC123", 150000m, "Thanh toan ve test", "127.0.0.1");
        var query = ParseQuery(url);

        var isValid = service.ValidateSignature(query);

        isValid.Should().BeTrue();
        query["vnp_TmnCode"].Should().Be(TmnCode);
        query["vnp_Amount"].Should().Be("15000000"); // 150.000 VNĐ * 100
    }

    [Fact]
    public void ValidateSignature_WhenAmountIsTampered_ShouldReturnFalse()
    {
        var service = CreateService();

        var url = service.CreatePaymentUrl("WB20261002ABC123", 150000m, "Thanh toan ve test", "127.0.0.1");
        var query = ParseQuery(url);

        // Giả mạo số tiền sau khi đã ký, không ký lại -> chữ ký phải không còn khớp
        query["vnp_Amount"] = "1";

        var isValid = service.ValidateSignature(query);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateSignature_WhenSecureHashMissing_ShouldReturnFalse()
    {
        var service = CreateService();

        var url = service.CreatePaymentUrl("WB20261002ABC123", 150000m, "Thanh toan ve test", "127.0.0.1");
        var query = ParseQuery(url);
        query.Remove("vnp_SecureHash");

        var isValid = service.ValidateSignature(query);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValidTmnCode_WhenMatchesConfiguredValue_ShouldReturnTrue()
    {
        var service = CreateService();

        service.IsValidTmnCode(TmnCode).Should().BeTrue();
    }

    [Fact]
    public void IsValidTmnCode_WhenDoesNotMatch_ShouldReturnFalse()
    {
        var service = CreateService();

        service.IsValidTmnCode("SOME_OTHER_MERCHANT_CODE").Should().BeFalse();
    }
}
