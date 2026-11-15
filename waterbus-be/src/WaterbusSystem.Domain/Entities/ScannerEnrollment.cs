using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>Credential generation and station snapshot for reconciliation.</summary>
public sealed class ScannerEnrollment : BaseEntity
{
    public Guid ScannerDeviceId { get; init; }
    public ScannerDevice? ScannerDevice { get; init; }
    public Guid StationId { get; init; }
    public string CredentialHash { get; init; } = string.Empty;
    public DateTimeOffset PairedAt { get; init; }
    public DateTimeOffset? EndedAt { get; set; }
}
