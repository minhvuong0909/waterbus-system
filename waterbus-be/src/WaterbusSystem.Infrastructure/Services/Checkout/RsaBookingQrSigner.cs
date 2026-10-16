using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.Checkout;

/// <summary>JWS RS256 = RSA SHA-256 PKCS#1 v1.5; không dùng HMAC/public-key hash làm chữ ký.</summary>
public sealed class RsaBookingQrSigner(IConfiguration configuration) : IBookingQrSigner
{
    public BookingQrCredential Sign(string publicBookingId, int version, Guid tripId, DateTimeOffset issuedAt)
    {
        using var rsa = ReadPrivateKey();
        var kid = ReadKeyId();
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid }));
        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(new { pbid = publicBookingId, ver = version,
            tid = tripId, iat = issuedAt.ToUnixTimeSeconds() }));
        var input = header + "." + body;
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new BookingQrCredential(input + "." + Encode(signature), "RS256", kid, version);
    }

    public BookingQrPublicKey GetPublicKey()
    {
        using var rsa = ReadPrivateKey();
        return new BookingQrPublicKey(ReadKeyId(), "RS256", rsa.ExportSubjectPublicKeyInfoPem());
    }

    private string ReadKeyId() => configuration["Qr:KeyId"] ?? "waterbus-booking-v1";
    private RSA ReadPrivateKey()
    {
        var pem = configuration["Qr:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(pem)) throw new InvalidOperationException("Thiếu Qr:PrivateKeyPem; cấu hình bằng User Secrets/biến môi trường.");
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048) throw new InvalidOperationException("QR RSA key phải ít nhất 2048 bit.");
            return rsa;
        }
        catch { rsa.Dispose(); throw; }
    }
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
