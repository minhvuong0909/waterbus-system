using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Rules;
using WaterbusSystem.Application.Validators.Captains;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using Xunit;

namespace WaterbusSystem.UnitTests.Captains;

public sealed class CaptainRulesTests
{
    [Fact]
    public void DeadlineIsRelativeToStaffOpening_NotDepartureOrWallClock()
    {
        var staff = DateTimeOffset.Parse("2026-11-16T06:30:00+07:00");
        Assert.Equal(staff.AddMinutes(-20), CaptainRules.RequiredAt(staff, 20));
        Assert.False(CaptainRules.IsLate(staff.AddMinutes(-20), CaptainRules.RequiredAt(staff, 20)));
        Assert.True(CaptainRules.IsLate(staff.AddMinutes(-20).AddTicks(1), CaptainRules.RequiredAt(staff, 20)));
        Assert.Equal(staff.AddMinutes(-45), CaptainRules.RequiredAt(staff, 45));
        Assert.Throws<ArgumentOutOfRangeException>(() => CaptainRules.RequiredAt(staff, 0));
    }
    [Fact]
    public void LicenseExpiresAtEndOfVietnamDate()
    {
        var lastTick = DateTimeOffset.Parse("2026-11-16T23:59:59.9999999+07:00");
        Assert.True(CaptainRules.IsEligible(true, true, CaptainStatus.Active, "ABC", new DateOnly(2026, 11, 16), lastTick));
        Assert.False(CaptainRules.IsEligible(true, true, CaptainStatus.Active, "ABC", new DateOnly(2026, 11, 16), lastTick.AddTicks(1)));
        Assert.False(CaptainRules.IsEligible(true, false, CaptainStatus.Active, "ABC", DateOnly.MaxValue, lastTick));
        Assert.False(CaptainRules.IsEligible(true, true, CaptainStatus.Suspended, "ABC", DateOnly.MaxValue, lastTick));
        Assert.False(CaptainRules.IsEligible(false, true, CaptainStatus.Active, "ABC", DateOnly.MaxValue, lastTick));
        Assert.False(CaptainRules.IsEligible(true, true, CaptainStatus.Active, null, null, lastTick));
    }
    [Theory]
    [InlineData(TripStatus.Boarding, true)] [InlineData(TripStatus.EnRoute, true)] [InlineData(TripStatus.Suspended, true)]
    [InlineData(TripStatus.Scheduled, false)] [InlineData(TripStatus.Cancelled, false)] [InlineData(TripStatus.Completed, false)]
    public void AssignmentBlocksOnlyOperationalTrips(TripStatus status, bool blocked) => Assert.Equal(blocked, CaptainRules.BlocksAssignmentChange(status));
    [Fact]
    public void RequestsRequireProfileAndReason()
    {
        var validator = new CreateCaptainRequestValidator();
        Assert.True(validator.Validate(new CreateCaptainRequest("Captain", "captain@example.com", "0901234567", "LICENSE", DateOnly.MaxValue, "Captain1234")).IsValid);
        Assert.False(validator.Validate(new CreateCaptainRequest("", "bad", "phone", "", default, "")).IsValid);
        Assert.False(new RecordCaptainAttendanceRequestValidator().Validate(new RecordCaptainAttendanceRequest(Guid.Empty, Guid.Empty, "")).IsValid);
        Assert.False(new ChangeCaptainStatusRequestValidator().Validate(new ChangeCaptainStatusRequest((CaptainStatus)99, "")).IsValid);
        Assert.True(new AssignCaptainRequestValidator().Validate(new AssignCaptainRequest(null, "Admin removes assignment")).IsValid);
    }
}
