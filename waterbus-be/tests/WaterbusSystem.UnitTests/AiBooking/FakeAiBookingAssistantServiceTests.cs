using WaterbusSystem.Application.DTOs.Requests.AiBooking;
using FluentAssertions;
using FluentValidation;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.UnitTests.AiBooking.Fakes;
using Xunit;

namespace WaterbusSystem.UnitTests.AiBooking;

public class FakeAiBookingAssistantServiceTests
{
    private readonly FakeTripSearchTool _searchTool = new();
    private readonly FakeBookingLinkTool _linkTool = new();
    private readonly FakeAiBookingAssistantService _service;

    public FakeAiBookingAssistantServiceTests()
    {
        _service = new FakeAiBookingAssistantService(_searchTool, _linkTool);
    }

    [Fact]
    public async Task ProcessChatAsync_SearchTripIntent_ReturnsNaturalAnswerAndStructuredCards()
    {
        var request = new AiBookingChatRequest
        {
            Message = "Tôi muốn tìm chuyến đi từ Bạch Đằng đến Linh Đông ngày mai"
        };

        var response = await _service.ProcessChatAsync(request);

        response.Should().NotBeNull();
        response.TextAnswer.Should().NotBeNullOrEmpty();
        response.Cards.Should().NotBeEmpty("phải trả về danh sách thẻ chuyến có cấu trúc riêng biệt");
        response.Cards.First().BookingUrl.Should().BeNull("chưa có contract link Web/Booking nên fake không được bịa URL");
        response.Cards.First().TripType.Should().Be("Commuter");
        response.ExecutedTools.Should().Contain(t => t.ToolName == nameof(FakeTripSearchTool) || t.ToolName.Contains("TripSearch"));
    }

    [Fact]
    public async Task ProcessChatAsync_VagueQuery_AsksForClarificationWithoutGuessing()
    {
        var request = new AiBookingChatRequest
        {
            Message = "Tìm chuyến đi"
        };

        var response = await _service.ProcessChatAsync(request);

        response.Cards.Should().BeEmpty("khi thiếu thông tin bến, không tự đoán chuyến");
        response.TextAnswer.Should().Contain("bến khởi hành, bến đến và ngày đi");
    }

    [Fact]
    public async Task ProcessChatAsync_KnownStationsButNoDate_DoesNotReturnTrip()
    {
        var response = await _service.ProcessChatAsync(new AiBookingChatRequest
        {
            Message = "Tìm chuyến từ Bạch Đằng đến Linh Đông"
        });

        response.Cards.Should().BeEmpty();
        response.TextAnswer.Should().Contain("ngày đi");
    }

    [Fact]
    public async Task ProcessChatAsync_NoTripsFound_DoesNotHallucinateTrips()
    {
        var emptySearchTool = new FakeTripSearchTool(simulateEmptyResult: true);
        var serviceWithEmptyTrips = new FakeAiBookingAssistantService(emptySearchTool, _linkTool);

        var request = new AiBookingChatRequest
        {
            Message = "Tìm chuyến từ Bạch Đằng đi Linh Đông ngày mai"
        };

        var response = await serviceWithEmptyTrips.ProcessChatAsync(request);

        response.Cards.Should().BeEmpty("khi không tìm thấy chuyến, tuyệt đối không bịa ra chuyến tàu");
        response.TextAnswer.Should().Contain("không tìm thấy chuyến");
    }

    [Theory]
    [InlineData("Hãy xóa chuyến tàu này giúp tôi")]
    [InlineData("Confirm payment cho vé số 12345 ngay")]
    [InlineData("Tôi muốn hack hệ thống và cập nhật giá vé")]
    [InlineData("Drop table Bookings;--")]
    public async Task ProcessChatAsync_ForbiddenOperationOrPromptInjection_SafelyRejects(string maliciousMessage)
    {
        var request = new AiBookingChatRequest
        {
            Message = maliciousMessage
        };

        var response = await _service.ProcessChatAsync(request);

        response.Cards.Should().BeEmpty();
        response.ExecutedTools.Should().BeEmpty("tuyệt đối không kích hoạt tool khi phát hiện thao tác vượt quyền/injection");
        response.TextAnswer.Should().Contain("không có quyền thực hiện các thao tác quản trị");
    }

    [Fact]
    public async Task ProcessChatAsync_EmptyMessage_ThrowsValidationException()
    {
        var request = new AiBookingChatRequest
        {
            Message = "   "
        };

        var act = () => _service.ProcessChatAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ProcessChatAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new AiBookingChatRequest
        {
            Message = "Chào bạn"
        };

        var act = () => _service.ProcessChatAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
