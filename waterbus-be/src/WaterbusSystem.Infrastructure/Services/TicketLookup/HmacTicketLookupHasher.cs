using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.TicketLookup;

/// <summary>
/// HMAC-SHA256 với khoá dẫn xuất từ JwtSettings:Secret (tách miền bằng nhãn riêng nên không dùng chung khoá với JWT).
/// Kết quả hex thường; so sánh bằng <see cref="CryptographicOperations.FixedTimeEquals"/>.
/// </summary>
public sealed class HmacTicketLookupHasher : ITicketLookupHasher
{
    private readonly byte[] _key;

    public HmacTicketLookupHasher(IConfiguration configuration)
    {
        var secret = configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("Thiếu cấu hình JwtSettings:Secret.");
        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes("waterbus:ticket-lookup:v1"));
    }

    public string Hash(string purpose, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(purpose + "\n" + value))).ToLowerInvariant();

    public bool Equals(string hashA, string hashB) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hashA), Encoding.UTF8.GetBytes(hashB));
}
