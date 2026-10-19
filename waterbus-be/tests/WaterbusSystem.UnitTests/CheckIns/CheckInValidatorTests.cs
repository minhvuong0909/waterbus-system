using FluentAssertions;
using WaterbusSystem.Application.Features.CheckIns.Commands.SetCheckInWindow;
using WaterbusSystem.Application.Features.CheckIns.Commands.SubmitCheckIn;
using WaterbusSystem.Application.Features.CheckIns.Commands.SyncCheckInEvents;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

public class CheckInValidatorTests
{
    [Fact]
    public void SyncCheckInEvents_WhenBatchIsEmpty_ShouldFailValidation()
    {
        var validator = new SyncCheckInEventsCommandValidator();

        var result = validator.Validate(new SyncCheckInEventsCommand(Array.Empty<CheckInEventInput>()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Events");
    }

    [Fact]
    public void SyncCheckInEvents_WhenAnEventIsMissingClientEventId_ShouldFailValidation()
    {
        var validator = new SyncCheckInEventsCommandValidator();
        var events = new[] { new CheckInEventInput(Guid.NewGuid(), Guid.NewGuid(), "", DateTimeOffset.UtcNow, null) };

        var result = validator.Validate(new SyncCheckInEventsCommand(events));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SetCheckInWindow_WhenCloseAtIsBeforeOpenAt_ShouldFailValidation()
    {
        var now = DateTimeOffset.UtcNow;
        var validator = new SetCheckInWindowCommandValidator();

        var result = validator.Validate(new SetCheckInWindowCommand(Guid.NewGuid(), now, now.AddMinutes(-1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CheckInCloseAt");
    }

    [Fact]
    public void SubmitCheckIn_WhenClientEventIdIsEmpty_ShouldFailValidation()
    {
        var validator = new SubmitCheckInCommandValidator();

        var result = validator.Validate(new SubmitCheckInCommand(Guid.NewGuid(), Guid.NewGuid(), "", DateTimeOffset.UtcNow, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ClientEventId");
    }
}
