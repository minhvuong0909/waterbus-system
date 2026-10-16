namespace WaterbusSystem.Application.Common.Interfaces;

public record BookingQrCredential(string Payload, string Algorithm, string KeyId, int CredentialVersion);
public record BookingQrPublicKey(string Kid, string Algorithm, string PublicKeyPem);
/// <summary>QR tĩnh per-Booking; key riêng chỉ được dùng ở Infrastructure.</summary>
public interface IBookingQrSigner
{
    BookingQrCredential Sign(string publicBookingId, int version, Guid tripId, DateTimeOffset issuedAt);
    BookingQrPublicKey GetPublicKey();
}
