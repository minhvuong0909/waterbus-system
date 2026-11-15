namespace WaterbusSystem.Application.Interfaces.Services;

public interface IScannerSecrets
{
    string Generate();
    string Hash(string secret);
    bool Matches(string secret, string? hash);
}
