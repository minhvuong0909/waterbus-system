using FluentValidation;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Features.AiBooking.Validators;

namespace WaterbusSystem.UnitTests.AiBooking.Fakes;

/// <summary>
/// Fake implementation của IAiBookingAssistantService phục vụ kiểm thử đơn vị độc lập.
/// Mô phỏng đầy đủ chu trình:
/// - Validate request chat
/// - Chặn prompt injection và các hành vi vượt quyền (BR-AI-01: không ghi DB, không thanh toán)
/// - Trích xuất tiêu chí và điều phối gọi ITripSearchTool và IBookingLinkTool
/// - Đảm bảo dữ liệu Trip Card có cấu trúc tách biệt văn bản tự nhiên, không tự bịa dữ liệu khi kết quả rỗng.
/// </summary>
public class FakeAiBookingAssistantService : IAiBookingAssistantService
{
    private readonly ITripSearchTool _tripSearchTool;
    private readonly IBookingLinkTool _bookingLinkTool;
    private readonly IValidator<AiBookingChatRequest> _validator;

    private static readonly string[] ForbiddenKeywords =
    {
        "xóa chuyến", "delete trip", "confirm payment", "thanh toán", "drop table",
        "cập nhật giá", "hack", "admin bypass", "tự tạo vé"
    };

    public FakeAiBookingAssistantService(
        ITripSearchTool tripSearchTool,
        IBookingLinkTool bookingLinkTool,
        IValidator<AiBookingChatRequest>? validator = null)
    {
        _tripSearchTool = tripSearchTool;
        _bookingLinkTool = bookingLinkTool;
        _validator = validator ?? new AiBookingChatRequestValidator();
    }

    public async Task<AiBookingChatResponse> ProcessChatAsync(
        AiBookingChatRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Validate request
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var message = request.Message.Trim();
        var lowerMsg = message.ToLowerInvariant();

        // 2. Chặn Prompt Injection / Hành vi vượt quyền (BR-AI-01)
        if (ForbiddenKeywords.Any(k => lowerMsg.Contains(k)))
        {
            return new AiBookingChatResponse(
                TextAnswer: "Xin lỗi quý khách, tôi là trợ lý tra cứu lịch chạy và hướng dẫn đặt vé của Saigon Waterbus. " +
                            "Tôi không có quyền thực hiện các thao tác quản trị, thanh toán trực tiếp hoặc thay đổi dữ liệu nghiệp vụ.",
                Cards: Array.Empty<AiTripCardDto>(),
                ExecutedTools: Array.Empty<AiToolExecutionDto>());
        }

        var executedTools = new List<AiToolExecutionDto>();

        // 3. Phân tích intent: Tìm kiếm chuyến tàu
        var isSearchIntent = lowerMsg.Contains("tìm chuyến") ||
                             lowerMsg.Contains("giờ tàu") ||
                             lowerMsg.Contains("lịch chạy") ||
                             lowerMsg.Contains("chuyến đi") ||
                             lowerMsg.Contains("bạch đằng") ||
                             lowerMsg.Contains("linh đông") ||
                             lowerMsg.Contains("bình an");

        if (isSearchIntent)
        {
            var criteria = ExtractSearchCriteria(message, request.PreferredServiceType);

            var searchResult = await _tripSearchTool.SearchTripsAsync(criteria, cancellationToken);

            executedTools.Add(new AiToolExecutionDto(
                ToolName: nameof(ITripSearchTool),
                ArgumentsSummary: $"Dep={criteria.DepartureStationName}, Arr={criteria.ArrivalStationName}, Type={criteria.TripType}",
                Success: searchResult.IsSuccess,
                ExecutionNote: searchResult.Message));

            if (searchResult.RequiresMoreClarification)
            {
                return new AiBookingChatResponse(
                    TextAnswer: "Dạ, để tìm chuyến chính xác, quý khách vui lòng cho biết bến khởi hành, bến đến và ngày đi mong muốn.",
                    Cards: Array.Empty<AiTripCardDto>(),
                    ExecutedTools: executedTools);
            }

            if (searchResult.Trips.Count == 0)
            {
                // Không có chuyến: trả lời rõ ràng, TUYỆT ĐỐI không bịa chuyến
                return new AiBookingChatResponse(
                    TextAnswer: "Rất tiếc, hiện tại không tìm thấy chuyến tàu nào phù hợp với yêu cầu của quý khách. Quý khách vui lòng thử chọn ngày khác hoặc bến khác nhé.",
                    Cards: Array.Empty<AiTripCardDto>(),
                    ExecutedTools: executedTools);
            }

            // Link chỉ xuất hiện nếu tool trả kết quả hợp lệ; fake không tự suy ra route Web.
            var enrichedCards = new List<AiTripCardDto>();
            foreach (var trip in searchResult.Trips)
            {
                var linkResult = await _bookingLinkTool.BuildBookingLinkAsync(new BookingLinkCriteria
                {
                    TripId = trip.TripId
                }, cancellationToken);

                enrichedCards.Add(trip with { BookingUrl = linkResult.IsValid ? linkResult.BookingUrl : null });
            }

            return new AiBookingChatResponse(
                TextAnswer: $"Dạ, tôi tìm thấy {enrichedCards.Count} chuyến tàu phù hợp cho quý khách. Quý khách có thể xem chi tiết từng chuyến bên dưới.",
                Cards: enrichedCards,
                ExecutedTools: executedTools);
        }

        // 4. Phản hồi chào hỏi thông thường
        return new AiBookingChatResponse(
            TextAnswer: "Xin chào quý khách! Tôi là trợ lý ảo Saigon Waterbus. Tôi có thể giúp quý khách tra cứu lịch chạy tàu, tìm chuyến phù hợp và cung cấp link đặt vé trực tuyến. Quý khách muốn đi từ bến nào ạ?",
            Cards: Array.Empty<AiTripCardDto>(),
            ExecutedTools: executedTools);
    }

    private static TripSearchCriteria ExtractSearchCriteria(string message, string? preferredServiceType)
    {
        var lower = message.ToLowerInvariant();
        string? dep = null;
        string? arr = null;

        if (lower.Contains("bạch đằng")) dep = "Bạch Đằng";
        if (lower.Contains("linh đông")) arr = "Linh Đông";
        if (lower.Contains("bình an"))
        {
            if (dep == null) dep = "Bình An";
            else arr = "Bình An";
        }

        DateOnly? departureDate = null;
        var localToday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (lower.Contains("ngày mai")) departureDate = localToday.AddDays(1);
        else if (lower.Contains("hôm nay")) departureDate = localToday;

        return new TripSearchCriteria
        {
            DepartureStationName = dep,
            ArrivalStationName = arr,
            DepartureDate = departureDate,
            TripType = preferredServiceType
        };
    }
}
