using FluentAssertions;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Features.AiBooking.Validators;
using Xunit;

namespace WaterbusSystem.UnitTests.AiBooking;

public class AiBookingValidatorsTests
{
    private readonly AiBookingChatRequestValidator _chatRequestValidator = new();
    private readonly TripSearchCriteriaValidator _tripSearchValidator = new();
    private readonly BookingLinkCriteriaValidator _bookingLinkValidator = new();

    // ── AiBookingChatRequestValidator ────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChatRequestValidator_EmptyMessage_ShouldFail(string? invalidMessage)
    {
        var request = new AiBookingChatRequest { Message = invalidMessage! };

        var result = _chatRequestValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message");
    }

    [Fact]
    public void ChatRequestValidator_MessageTooLong_ShouldFail()
    {
        var longMessage = new string('a', 1001);
        var request = new AiBookingChatRequest { Message = longMessage };

        var result = _chatRequestValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("1000 ký tự"));
    }

    [Theory]
    [InlineData("Commuter")]
    [InlineData("Sightseeing")]
    [InlineData(null)]
    public void ChatRequestValidator_ValidServiceType_ShouldPass(string? serviceType)
    {
        var request = new AiBookingChatRequest
        {
            Message = "Tìm chuyến lúc 9h sáng",
            PreferredServiceType = serviceType
        };

        var result = _chatRequestValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("vi", true)]
    [InlineData("en", true)]
    [InlineData("fr", false)]
    public void ChatRequestValidator_OnlySupportsWebsiteLanguages(string? languageCode, bool valid)
    {
        var result = _chatRequestValidator.Validate(new AiBookingChatRequest
        {
            Message = "Find trips",
            LanguageCode = languageCode
        });

        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void ChatRequestValidator_InvalidServiceType_ShouldFail()
    {
        var request = new AiBookingChatRequest
        {
            Message = "Tìm chuyến tàu",
            PreferredServiceType = "VIP_Luxury"
        };

        var result = _chatRequestValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PreferredServiceType");
    }

    [Fact]
    public void ChatRequestValidator_LegacyRegularType_ShouldFail()
    {
        var result = _chatRequestValidator.Validate(new AiBookingChatRequest
        {
            Message = "Tìm chuyến",
            PreferredServiceType = "Regular"
        });

        result.IsValid.Should().BeFalse("Domain TripType chỉ có Commuter và Sightseeing");
    }

    [Fact]
    public void ChatRequestValidator_ValidSearchContext_ShouldPass()
    {
        var result = _chatRequestValidator.Validate(new AiBookingChatRequest
        {
            Message = "Sớm hơn một chút",
            SearchContext = new TripSearchCriteria
            {
                DepartureStationName = "Bạch Đằng",
                PassengerCount = 2,
                PreferredDepartureTime = new TimeOnly(16, 0)
            }
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ChatRequestValidator_OversizedClientContext_ShouldFail()
    {
        var result = _chatRequestValidator.Validate(new AiBookingChatRequest
        {
            Message = "Sớm hơn một chút",
            SearchContext = new TripSearchCriteria { DepartureStationName = new string('a', 101) }
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("DepartureStationName"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChatRequestValidator_EmptyPendingQuestion_ShouldFail(string originalMessage)
    {
        var result = _chatRequestValidator.Validate(new AiBookingChatRequest
        {
            Message = "lọc tiếp",
            PendingClarification = new AiSearchScopeClarification(originalMessage)
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("OriginalMessage"));
    }

    [Fact]
    public void ChatRequestValidator_OversizedPendingQuestion_ShouldFail()
    {
        var result = _chatRequestValidator.Validate(new AiBookingChatRequest
        {
            Message = "lọc tiếp",
            PendingClarification = new AiSearchScopeClarification(new string('a', 1001))
        });

        result.IsValid.Should().BeFalse();
    }

    // ── TripSearchCriteriaValidator ─────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(51)]
    public void TripSearchCriteriaValidator_InvalidPassengerCount_ShouldFail(int count)
    {
        var criteria = new TripSearchCriteria { PassengerCount = count };

        var result = _tripSearchValidator.Validate(criteria);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PassengerCount");
    }

    [Fact]
    public void TripSearchCriteriaValidator_DateInPast_ShouldFail()
    {
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));
        var criteria = new TripSearchCriteria { DepartureDate = pastDate };

        var result = _tripSearchValidator.Validate(criteria);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DepartureDate");
    }

    [Fact]
    public void TripSearchCriteriaValidator_YesterdayInVietnam_ShouldFail()
    {
        var localToday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var result = _tripSearchValidator.Validate(new TripSearchCriteria
        {
            DepartureDate = localToday.AddDays(-1)
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TripSearchCriteriaValidator_ValidCriteria_ShouldPass()
    {
        var criteria = new TripSearchCriteria
        {
            DepartureStationName = "Bạch Đằng",
            ArrivalStationName = "Linh Đông",
            DepartureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            PassengerCount = 4,
            TripType = "Commuter"
        };

        var result = _tripSearchValidator.Validate(criteria);

        result.IsValid.Should().BeTrue();
    }

    // ── BookingLinkCriteriaValidator ────────────────────────────────────────

    [Fact]
    public void BookingLinkCriteriaValidator_EmptyTripId_ShouldFail()
    {
        var criteria = new BookingLinkCriteria
        {
            TripId = Guid.Empty
        };

        var result = _bookingLinkValidator.Validate(criteria);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TripId");
    }

    [Fact]
    public void BookingLinkCriteriaValidator_ValidTripId_ShouldPass()
    {
        var criteria = new BookingLinkCriteria
        {
            TripId = Guid.NewGuid(),
            PassengerCount = 2
        };

        var result = _bookingLinkValidator.Validate(criteria);

        result.IsValid.Should().BeTrue();
    }
}
