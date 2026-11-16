using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

public sealed class StaffStationShift : BaseEntity
{
    public Guid StaffAccountId { get; init; }
    public Guid StationId { get; init; }
    public Station? Station { get; init; }
    public DateTimeOffset ActiveFrom { get; init; }
    public DateTimeOffset ActiveUntil { get; init; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Thời điểm ca thực sự kết thúc: hết giờ phân ca hoặc lúc Admin thu hồi, tuỳ cái nào đến trước.</summary>
    public DateTimeOffset EffectiveEnd => RevokedAt is { } revoked && revoked < ActiveUntil ? revoked : ActiveUntil;

    public bool Covers(DateTimeOffset at) =>
        ActiveFrom <= at && at < ActiveUntil && (RevokedAt is null || at < RevokedAt);

    public void Revoke(DateTimeOffset now)
    {
        if (now < ActiveUntil) RevokedAt ??= now;
    }
}
