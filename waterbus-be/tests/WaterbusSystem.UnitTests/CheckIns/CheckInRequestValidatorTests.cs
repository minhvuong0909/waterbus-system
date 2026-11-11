using FluentAssertions;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.Validators.CheckIns;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

public class CheckInRequestValidatorTests
{
    [Fact]
    public void SyncCheckInEvents_WhenBatchIsEmpty_ShouldFailValidation()
    {
        var validator = new SyncCheckInEventsRequestValidator();

        var result = validator.Validate(new SyncCheckInEventsRequest(Array.Empty<CheckInEventInput>()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Events");
    }

    [Fact]
    public void SyncCheckInEvents_WhenAnEventIsMissingClientEventId_ShouldFailValidation()
    {
        var validator = new SyncCheckInEventsRequestValidator();
        var events = new[] { new CheckInEventInput(Guid.NewGuid(), Guid.NewGuid(), "", DateTimeOffset.UtcNow, null) };

        var result = validator.Validate(new SyncCheckInEventsRequest(events));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SetCheckInWindow_WhenCloseAtIsBeforeOpenAt_ShouldFailValidation()
    {
        var now = DateTimeOffset.UtcNow;
        var validator = new SetCheckInWindowRequestValidator();

        var result = validator.Validate(new SetCheckInWindowRequest(now, now.AddMinutes(-1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CheckInCloseAt");
    }

    [Fact]
    public void SubmitCheckIn_WhenClientEventIdIsEmpty_ShouldFailValidation()
    {
        var validator = new SubmitCheckInRequestValidator();

        var result = validator.Validate(new SubmitCheckInRequest(Guid.NewGuid(), Guid.NewGuid(), "", DateTimeOffset.UtcNow, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ClientEventId");
    }
}
