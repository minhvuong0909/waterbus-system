using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Identity;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

public sealed class StationScannerDomainTests
{
    [Fact]
    public void CreationBindsManyPendingScannersToOneStation_AndActiveScannerCannotTransfer()
    {
        var station = new Station { Code = "BD" };
        var first = ScannerDevice.Register(station, "SC-BD-001", "Gate one");
        var second = ScannerDevice.Register(station, "SC-BD-002", "Gate two");
        Assert.Equal(station.Id, first.StationId);
        Assert.Equal(station.Id, second.StationId);
        Assert.NotEqual(first.Id, second.Id);
        Assert.False(first.IsActive);
        Assert.False(second.IsActive);

        first.IsActive = true;
        Assert.Throws<InvalidOperationException>(() => first.TransferTo(new Station()));
        Assert.Equal(station.Id, first.StationId);
    }

    [Fact]
    public void ShiftBoundsAreHalfOpen_AndRevocationPreservesEarlierAuthorization()
    {
        var start = new DateTimeOffset(2026, 11, 16, 7, 0, 0, TimeSpan.FromHours(7));
        var end = start.AddHours(4);
        var shift = new StaffStationShift { ActiveFrom = start, ActiveUntil = end };
        Assert.False(shift.Covers(start.AddTicks(-1)));
        Assert.True(shift.Covers(start));
        Assert.False(shift.Covers(end));

        var revoked = start.AddHours(2);
        shift.Revoke(revoked);
        shift.Revoke(revoked.AddMinutes(1));
        Assert.Equal(revoked, shift.RevokedAt);
        Assert.Equal(end, shift.ActiveUntil);
        Assert.True(shift.Covers(revoked.AddTicks(-1)));
        Assert.False(shift.Covers(revoked));
    }

    [Fact]
    public void CredentialsUseIndependent256BitValues_AndOnlyTheCorrectSecretMatches()
    {
        var secrets = new ScannerSecrets();
        var secret = secrets.Generate();
        var other = secrets.Generate();
        var hash = secrets.Hash(secret);
        Assert.Equal(32, Convert.FromHexString(secret).Length);
        Assert.NotEqual(secret, other);
        Assert.NotEqual(secret, hash);
        Assert.True(secrets.Matches(secret, hash));
        Assert.False(secrets.Matches(other, hash));
        Assert.False(secrets.Matches(secret, null));
    }
}
