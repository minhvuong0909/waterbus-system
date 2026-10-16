using System.ComponentModel;
using System.Globalization;
using FluentValidation;
using Microsoft.SemanticKernel;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Features.AiBooking.Validators;

namespace WaterbusSystem.Infrastructure.Services.AiBooking.Plugins;

/// <summary>
/// Semantic Kernel Plugin cung cấp các công cụ tra cứu chuyến và tạo link đặt vé cho AI Assistant.
/// Ranh giới an toàn nghiêm ngặt (BR-AI-01):
/// - Chỉ ủy quyền cho ITripSearchTool và IBookingLinkTool.
/// - Không chứa các hàm sửa đổi CSDL, không tạo Booking hay thanh toán.
/// - Thu thập dữ liệu thẻ chuyến thật (InterceptedCards) trực tiếp từ kết quả tool,
///   ngăn chặn hoàn toàn việc AI tự bịa giờ, giá, bến hoặc URL.
/// </summary>
public class WaterbusBookingPlugin
{
    private readonly ITripSearchTool _tripSearchTool;
    private readonly IBookingLinkTool _bookingLinkTool;
    private readonly List<AiTripCardDto> _interceptedCards = new();
    private readonly List<AiToolExecutionDto> _executedTools = new();
    private readonly int _maxToolCalls;
    private int _toolCallCount;

    public bool SearchAttempted { get; private set; }
    public bool SearchNeedsClarification { get; private set; }
    public bool SearchFailed { get; private set; }

    public WaterbusBookingPlugin(
        ITripSearchTool tripSearchTool,
        IBookingLinkTool bookingLinkTool,
        int maxToolCalls = 5)
    {
        _tripSearchTool = tripSearchTool ?? throw new ArgumentNullException(nameof(tripSearchTool));
        _bookingLinkTool = bookingLinkTool ?? throw new ArgumentNullException(nameof(bookingLinkTool));
        _maxToolCalls = maxToolCalls > 0 ? maxToolCalls : throw new ArgumentOutOfRangeException(nameof(maxToolCalls));
    }

    /// <summary>
    /// Danh sách các thẻ chuyến thật thu thập được từ các lần gọi tool tra cứu thực tế.
    /// </summary>
    public IReadOnlyList<AiTripCardDto> InterceptedCards => _interceptedCards.AsReadOnly();

    /// <summary>
    /// Danh sách các lần gọi tool đã thực hiện trong phiên làm việc.
    /// </summary>
    public IReadOnlyList<AiToolExecutionDto> ExecutedTools => _executedTools.AsReadOnly();

    [KernelFunction("SearchTrips")]
    [Description("Tìm kiếm các chuyến tàu buýt đường sông (Saigon Waterbus) theo bến khởi hành, bến đến, ngày khởi hành và loại dịch vụ.")]
    public async Task<string> SearchTripsAsync(
        [Description("Tên bến khởi hành (ví dụ: 'Bến Bạch Đằng', 'Bình An'). Bắt buộc.")] string departureStation,
        [Description("Tên bến đến / cập bến (ví dụ: 'Bến Linh Đông', 'Thủ Thiêm'). Bắt buộc.")] string arrivalStation,
        [Description("Ngày khởi hành theo định dạng YYYY-MM-DD. Bắt buộc.")] string departureDate,
        [Description("Loại dịch vụ: 'Commuter' (tàu buýt thông thường) hoặc 'Sightseeing' (tàu ngắm cảnh).")] string? tripType = null,
        [Description("Số lượng hành khách (mặc định là 1, tối thiểu 1, tối đa 50).")] int? passengerCount = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryStartToolCall()) return "Đã đạt giới hạn gọi công cụ trong lượt hội thoại này.";

        if (string.IsNullOrWhiteSpace(departureStation) || string.IsNullOrWhiteSpace(arrivalStation))
        {
            SearchNeedsClarification = true;
            return "Thiếu thông tin bến khởi hành hoặc bến đến. Vui lòng hỏi khách để cung cấp đủ cả hai bến.";
        }

        if (!DateOnly.TryParseExact(departureDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            SearchNeedsClarification = true;
            return "Ngày khởi hành không hợp lệ. Vui lòng yêu cầu ngày theo định dạng YYYY-MM-DD.";
        }

        var normalizedType = NormalizeTripType(tripType);
        if ((!string.IsNullOrWhiteSpace(tripType) && normalizedType == null)
            || (passengerCount.HasValue && (passengerCount < 1 || passengerCount > 50)))
        {
            SearchNeedsClarification = true;
            return "Loại chuyến hoặc số lượng hành khách không hợp lệ; vui lòng hỏi lại khách.";
        }

        var criteria = new TripSearchCriteria
        {
            DepartureStationName = departureStation.Trim(),
            ArrivalStationName = arrivalStation.Trim(),
            DepartureDate = parsedDate,
            TripType = normalizedType,
            PassengerCount = passengerCount ?? 1
        };

        var validation = await new TripSearchCriteriaValidator().ValidateAsync(criteria, cancellationToken);
        if (!validation.IsValid)
        {
            SearchNeedsClarification = true;
            return "Tiêu chí tìm chuyến không hợp lệ; vui lòng hỏi lại ngày, bến và loại chuyến.";
        }

        SearchAttempted = true;
        var searchResult = await _tripSearchTool.SearchTripsAsync(criteria, cancellationToken);
        SearchNeedsClarification = searchResult.RequiresMoreClarification;
        SearchFailed = !searchResult.IsSuccess;

        _executedTools.Add(new AiToolExecutionDto(
            ToolName: "SearchTrips",
            ArgumentsSummary: "Tiêu chí tìm chuyến đã được kiểm tra",
            Success: searchResult.IsSuccess,
            ExecutionNote: null));

        if (searchResult.RequiresMoreClarification)
        {
            return "Cần thêm thông tin để tra cứu chuyến chính xác.";
        }

        if (!searchResult.IsSuccess)
            return "Hệ thống tìm chuyến đang gặp lỗi; vui lòng thử lại sau.";

        if (searchResult.Trips.Count == 0)
        {
            return "Không tìm thấy chuyến tàu nào phù hợp với yêu cầu trên lịch chạy chính thức.";
        }

        // Lưu thẻ chuyến thực tế để trả về cho UI
        foreach (var trip in searchResult.Trips)
        {
            if (!_interceptedCards.Any(c => c.TripId == trip.TripId))
            {
                // Search tool không có quyền cung cấp booking URL; giá chưa xác thực cũng không hiển thị.
                _interceptedCards.Add(trip with
                {
                    BookingUrl = null,
                    BasePrice = trip.IsPriceVerified ? trip.BasePrice : null
                });
            }
        }

        // Tạo tóm tắt văn bản trả về cho LLM tham khảo
        var summaryLines = searchResult.Trips.Select((t, index) =>
            $"{index + 1}. Tuyến '{t.RouteName}' (Mã: {t.TripId}): Khởi hành lúc {t.DepartureTime:HH:mm} tại {t.DepartureStationName}, đến {t.ArrivalStationName} lúc {t.ArrivalTime:HH:mm}. " +
            $"Còn {t.AvailableSeatsCount} ghế. Giá: {(t.IsPriceVerified && t.BasePrice.HasValue ? $"{t.BasePrice:N0} VNĐ" : "Chưa xác thực")}.");

        return $"Tìm thấy {searchResult.Trips.Count} chuyến tàu chính thức:\n" + string.Join("\n", summaryLines);
    }

    [KernelFunction("BuildBookingLink")]
    [Description("Tạo đường dẫn đặt vé chính thức cho một chuyến tàu hợp lệ dựa trên mã TripId.")]
    public async Task<string> BuildBookingLinkAsync(
        [Description("Mã định danh duy nhất của chuyến tàu (TripId dạng GUID). Bắt buộc.")] string tripId,
        [Description("Mã định danh bến lên tàu (GUID, tùy chọn).")] string? boardingStationId = null,
        [Description("Mã định danh bến xuống tàu (GUID, tùy chọn).")] string? disembarkingStationId = null,
        [Description("Số lượng hành khách (1 đến 50, tùy chọn).")] int? passengerCount = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryStartToolCall()) return "Đã đạt giới hạn gọi công cụ trong lượt hội thoại này.";

        if (!Guid.TryParse(tripId, out var parsedTripId) || parsedTripId == Guid.Empty)
        {
            return "Mã chuyến tàu (TripId) không hợp lệ.";
        }

        var cardIndex = _interceptedCards.FindIndex(c => c.TripId == parsedTripId);
        if (cardIndex < 0)
            return "Chỉ có thể tạo link cho chuyến vừa được công cụ tìm kiếm trả về.";

        if ((!string.IsNullOrWhiteSpace(boardingStationId) && !Guid.TryParse(boardingStationId, out _))
            || (!string.IsNullOrWhiteSpace(disembarkingStationId) && !Guid.TryParse(disembarkingStationId, out _))
            || (passengerCount.HasValue && (passengerCount < 1 || passengerCount > 50)))
            return "Tham số bến lên, bến xuống hoặc số hành khách không hợp lệ.";

        Guid? parsedBoarding = Guid.TryParse(boardingStationId, out var bId) ? bId : null;
        Guid? parsedDisembarking = Guid.TryParse(disembarkingStationId, out var dId) ? dId : null;

        var criteria = new BookingLinkCriteria
        {
            TripId = parsedTripId,
            BoardingStationId = parsedBoarding,
            DisembarkingStationId = parsedDisembarking,
            PassengerCount = passengerCount ?? 1
        };

        var validation = await new BookingLinkCriteriaValidator().ValidateAsync(criteria, cancellationToken);
        if (!validation.IsValid)
            return "Tham số đặt vé không hợp lệ.";

        var linkResult = await _bookingLinkTool.BuildBookingLinkAsync(criteria, cancellationToken);

        _executedTools.Add(new AiToolExecutionDto(
            ToolName: "BuildBookingLink",
            ArgumentsSummary: "Chuyến đã có trong kết quả tìm kiếm",
            Success: linkResult.IsValid,
            ExecutionNote: null));

        if (!linkResult.IsValid || string.IsNullOrWhiteSpace(linkResult.BookingUrl))
        {
            return "Chưa thể tạo liên kết đặt vé; vui lòng thử lại sau.";
        }

        if (!Uri.TryCreate(linkResult.BookingUrl, UriKind.Absolute, out var bookingUri)
            || bookingUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(bookingUri.UserInfo))
            return "Liên kết đặt vé không hợp lệ.";

        // Cập nhật URL đặt vé vào thẻ chuyến tương ứng
        _interceptedCards[cardIndex] = _interceptedCards[cardIndex] with { BookingUrl = linkResult.BookingUrl };

        return $"Đã tạo liên kết đặt vé chính thức: {linkResult.BookingUrl}";
    }

    private static string? NormalizeTripType(string? tripType)
    {
        if (string.IsNullOrWhiteSpace(tripType)) return null;

        if (string.Equals(tripType, "Commuter", StringComparison.OrdinalIgnoreCase))
        {
            return "Commuter";
        }

        if (string.Equals(tripType, "Sightseeing", StringComparison.OrdinalIgnoreCase))
        {
            return "Sightseeing";
        }

        return null;
    }

    private bool TryStartToolCall()
    {
        if (_toolCallCount >= _maxToolCalls) return false;
        _toolCallCount++;
        return true;
    }
}
