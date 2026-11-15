using System.Security.Cryptography;
using System.Text;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.Infrastructure.Identity;

public sealed class ScannerSecrets : IScannerSecrets
{
    public string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(secret)));

    public bool Matches(string secret, string? hash) => hash is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret)), Encoding.ASCII.GetBytes(hash));
}
