using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Infrastructure.Services.Payment;

namespace WaterbusSystem.UnitTests.Payments;

internal sealed class VerifiedVnPayFixture : IDisposable
{
    private readonly string _secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly HttpClient _client;
    public VnPayService Gateway { get; }
    public string QueryTransactionNo { get; set; } = "14000111";
    public string QueryStatus { get; set; } = "00";
    public string QueryType { get; set; } = "01";
    public long QueryAmount { get; set; }
    public string? QueryReference { get; set; }
    public bool SignQueryResponse { get; set; } = true;
    public int QueryCalls { get; private set; }

    public VerifiedVnPayFixture()
    {
        _client = new HttpClient(new MerchantHandler(this));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VnPay:TmnCode"] = "AUDITTEST",
            ["VnPay:HashSecret"] = _secret
        }).Build();
        Gateway = new VnPayService(configuration, _client, NullLogger<VnPayService>.Instance);
    }

    public Dictionary<string, string> Callback(string reference, decimal amount, string responseCode = "00",
        string status = "00", string transactionNo = "14000111")
    {
        var fields = new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = "AUDITTEST", ["vnp_TxnRef"] = reference,
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(CultureInfo.InvariantCulture),
            ["vnp_TransactionNo"] = transactionNo, ["vnp_ResponseCode"] = responseCode,
            ["vnp_TransactionStatus"] = status
        };
        SignCallback(fields);
        return fields;
    }

    public void SignCallback(Dictionary<string, string> fields)
    {
        var data = string.Join("&", fields.Where(p => p.Key != "vnp_SecureHash" && p.Value.Length > 0)
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => WebUtility.UrlEncode(p.Key) + "=" + WebUtility.UrlEncode(p.Value)));
        fields["vnp_SecureHash"] = Sign(data);
    }

    private string Sign(string data) => Convert.ToHexString(HMACSHA512.HashData(
        Encoding.UTF8.GetBytes(_secret), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

    private sealed class MerchantHandler(VerifiedVnPayFixture fixture) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = JsonSerializer.Deserialize<Dictionary<string, string>>(await request.Content!.ReadAsStringAsync(ct))!;
            if (body["vnp_Command"] != "querydr") throw new InvalidOperationException("Unexpected merchant request.");
            fixture.QueryCalls++;
            var response = new Dictionary<string, string>
            {
                ["vnp_ResponseId"] = "query-response", ["vnp_Command"] = "querydr",
                ["vnp_ResponseCode"] = "00", ["vnp_Message"] = "Query completed",
                ["vnp_TmnCode"] = "AUDITTEST", ["vnp_TxnRef"] = fixture.QueryReference ?? body["vnp_TxnRef"],
                ["vnp_Amount"] = fixture.QueryAmount.ToString(CultureInfo.InvariantCulture),
                ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = "20261029120000",
                ["vnp_TransactionNo"] = fixture.QueryTransactionNo, ["vnp_TransactionType"] = fixture.QueryType,
                ["vnp_TransactionStatus"] = fixture.QueryStatus, ["vnp_OrderInfo"] = "Query transaction",
                ["vnp_PromotionCode"] = "", ["vnp_PromotionAmount"] = ""
            };
            var names = new[] { "vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message", "vnp_TmnCode",
                "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo",
                "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo", "vnp_PromotionCode", "vnp_PromotionAmount" };
            response["vnp_SecureHash"] = fixture.SignQueryResponse
                ? fixture.Sign(string.Join("|", names.Select(n => response[n]))) : "invalid";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
            };
        }
    }

    public void Dispose() => _client.Dispose();
}
