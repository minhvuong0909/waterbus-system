param(
    [Parameter(Mandatory)][Guid]$OrderId,
    [Parameter(Mandatory)][string]$GuestToken,
    [string]$BaseUrl = 'http://127.0.0.1:5189'
)

# Chỉ giả lập merchant của môi trường demo TV2, không đọc secret VNPay thật.
$ErrorActionPreference = 'Stop'
$demoUri = [Uri]$BaseUrl
if ($demoUri.Host -notin @('127.0.0.1', 'localhost') -or $demoUri.Port -ne 5189) {
    throw 'Script chỉ dùng cho API demo local TV2 ở port 5189.'
}
$headers = @{ 'X-Guest-Token' = $GuestToken }
$payment = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/payments/create-vnpay-url" `
    -Headers $headers -ContentType 'application/json' -Body (@{ orderId = $OrderId.ToString() } | ConvertTo-Json)
# Đúng merchant/hash giả của demo; không ký webhook cho merchant sandbox/production.
if ($payment.paymentUrl -notmatch 'vnp_TmnCode=TESTCODE(?:&|$)') { throw 'API này không dùng merchant demo TESTCODE.' }
$data = [System.Collections.Generic.SortedDictionary[string,string]]::new([StringComparer]::Ordinal)
$data['vnp_Amount'] = ([long]([decimal]$payment.amount * 100)).ToString([Globalization.CultureInfo]::InvariantCulture)
$data['vnp_ResponseCode'] = '00'
$data['vnp_TmnCode'] = 'TESTCODE'
$data['vnp_TransactionNo'] = ([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()).ToString()
$data['vnp_TransactionStatus'] = '00'
$data['vnp_TxnRef'] = $payment.merchantReference
$query = ($data.GetEnumerator() | ForEach-Object {
    [Net.WebUtility]::UrlEncode($_.Key) + '=' + [Net.WebUtility]::UrlEncode($_.Value)
}) -join '&'
$hmac = [Security.Cryptography.HMACSHA512]::new([Text.Encoding]::UTF8.GetBytes('waterbus-smoke-only-no-bank-credentials'))
try {
    $digest = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($query))
    $signature = ([BitConverter]::ToString($digest)).Replace('-', '').ToLowerInvariant()
} finally { $hmac.Dispose() }
$result = Invoke-RestMethod -Uri "$BaseUrl/api/v1/payments/vnpay-ipn?$query&vnp_SecureHash=$signature"
if ($result.RspCode -notin @('00', '02')) { throw "IPN demo bị từ chối: $($result.RspCode)" }
$result
Invoke-RestMethod -Uri "$BaseUrl/api/v1/checkouts/$OrderId" -Headers $headers
