using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Infrastructure.Services.Payment;
using WaterbusSystem.Infrastructure.Services.Refunds;
using Xunit;

namespace WaterbusSystem.UnitTests.Security;

public sealed class VnPayMerchantTests
{
    private const string Secret = "unit-test-only-secret";
    private static string Sign(string input) => Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    private sealed class Handler(bool tamper = false, string? mismatchedField = null) : HttpMessageHandler
    {
        public Dictionary<string, string>? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = JsonSerializer.Deserialize<Dictionary<string, string>>(await request.Content!.ReadAsStringAsync(ct))!;
            var result = new Dictionary<string, string> { ["vnp_ResponseId"] = "test-response", ["vnp_Command"] = Body["vnp_Command"],
                ["vnp_ResponseCode"] = "00", ["vnp_Message"] = "accepted", ["vnp_TmnCode"] = "TESTCODE",
                ["vnp_TxnRef"] = Body["vnp_TxnRef"], ["vnp_Amount"] = Body.GetValueOrDefault("vnp_Amount", "1500000"),
                ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = "20261012120000", ["vnp_TransactionNo"] = "999",
                ["vnp_TransactionType"] = "03", ["vnp_TransactionStatus"] = "05", ["vnp_OrderInfo"] = Body["vnp_OrderInfo"] };
            if (mismatchedField is not null) result[mismatchedField] = "unrelated-operation";
            var keys = new List<string> { "vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message", "vnp_TmnCode",
                "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo", "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo" };
            if (Body["vnp_Command"] == "querydr") keys.AddRange(["vnp_PromotionCode", "vnp_PromotionAmount"]);
            result["vnp_SecureHash"] = Sign(string.Join("|", keys.Select(k => result.GetValueOrDefault(k, ""))));
            if (tamper) result["vnp_Amount"] = "1";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(result)) };
        }
    }
    private static VnPayService Service(Handler handler) => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["VnPay:TmnCode"] = "TESTCODE", ["VnPay:HashSecret"] = Secret }).Build(), new HttpClient(handler), NullLogger<VnPayService>.Instance);
    private static VnPayRefundRequest Request() => new() { RequestId = "01234567890123456789012345678901", MerchantReference = "original-ref",
        GatewayReference = "123", Amount = 15000, TransactionDate = "20261012110000", CreatedBy = "admin",
        IpAddress = "127.0.0.1", OrderInfo = "Refund waterbus", IsFullRefund = false };

    [Fact]
    public async Task RefundUsesStableRequestIdAndTheDocumentedPipeChecksum()
    {
        var handler = new Handler(); var result = await Service(handler).RefundAsync(Request());
        result.IsSuccess.Should().BeTrue(); result.IsPendingProcessing.Should().BeTrue();
        handler.Body!["vnp_RequestId"].Should().Be(Request().RequestId);
        var keys = new[] { "vnp_RequestId", "vnp_Version", "vnp_Command", "vnp_TmnCode", "vnp_TransactionType", "vnp_TxnRef",
            "vnp_Amount", "vnp_TransactionNo", "vnp_TransactionDate", "vnp_CreateBy", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo" };
        handler.Body["vnp_SecureHash"].Should().Be(Sign(string.Join("|", keys.Select(k => handler.Body[k]))));
    }

    [Fact]
    public async Task TamperedRefundResponseNeverConfirmsMoneyWasReturned()
    {
        var result = await Service(new Handler(tamper: true)).RefundAsync(Request());
        result.IsSuccess.Should().BeFalse(); result.ResponseCode.Should().Be("97");
    }

    [Theory]
    [InlineData("vnp_Command")]
    [InlineData("vnp_TransactionType")]
    public async Task SignedButUnrelatedRefundResponseIsRejected(string field)
    {
        var result = await Service(new Handler(mismatchedField: field)).RefundAsync(Request());
        result.IsSuccess.Should().BeFalse(); result.ResponseCode.Should().Be("97");
    }

    [Fact]
    public async Task PendingRefundResponseIsNotReportedSucceeded()
    {
        var gateway = new VnPayRefundGateway(Service(new Handler()), NullLogger<VnPayRefundGateway>.Instance);
        var r = Request();
        var outcome = await gateway.RefundAsync(new RefundGatewayRequest { RequestId = r.RequestId, MerchantReference = r.MerchantReference,
            GatewayReference = r.GatewayReference, Amount = r.Amount, TransactionDate = r.TransactionDate,
            CreatedBy = r.CreatedBy, IpAddress = r.IpAddress, OrderInfo = r.OrderInfo }, default);
        outcome.Outcome.Should().Be(RefundGatewayOutcome.Pending);
    }

    [Fact]
    public void PaymentUrlContainsOriginalAndExpiryTimesInVietnamTime()
    {
        var created = new DateTimeOffset(2026, 10, 12, 3, 0, 0, TimeSpan.Zero);
        var url = Service(new Handler()).CreatePaymentUrl("pay-ref", 15000, "Waterbus", "127.0.0.1", created, created.AddMinutes(15));
        url.Should().Contain("vnp_CreateDate=20261012100000").And.Contain("vnp_ExpireDate=20261012101500");
    }
}
