using WaterbusSystem.Domain.Common;
namespace WaterbusSystem.Domain.Entities;
/// <summary>Thiết bị quét QR tại bến (online hoặc offline)</summary>
public class ScannerDevice : BaseEntity
{
    public string DeviceCode { get; set; } = string.Empty;  // UNIQUE
    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? StationId { get; private set; }
    public Station? Station { get; private set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public string? InvitationHash { get; private set; }
    public DateTimeOffset? InvitationExpiresAt { get; private set; }

    public static ScannerDevice Register(Station station, string code, string? label) =>
        new() { StationId = station.Id, Station = station, DeviceCode = code, Label = label, IsActive = false };

    public void TransferTo(Station station)
    {
        if (IsActive) throw new InvalidOperationException("End the current enrollment before transferring the scanner.");
        Station = station;
        StationId = station.Id;
        ClearInvitation();
    }

    public void Invite(string hash, DateTimeOffset expiresAt)
    {
        InvitationHash = hash;
        InvitationExpiresAt = expiresAt;
    }

    public void ClearInvitation()
    {
        InvitationHash = null;
        InvitationExpiresAt = null;
    }

    public DateTimeOffset? LastPreloadedAt { get; set; }
}
