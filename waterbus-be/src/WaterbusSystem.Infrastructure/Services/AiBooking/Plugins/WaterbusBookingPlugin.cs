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
    private readonly TripSearchCriteria? _previousSearch;
    private readonly List<AiTripCardDto> _interceptedCards = new();
    private readonly List<string> _listedStations = new();
    private readonly List<AiToolExecutionDto> _executedTools = new();
    private readonly int _maxToolCalls;
    private int _toolCallCount;

    public bool SearchAttempted { get; private set; }
    public bool SearchNeedsClarification { get; private set; }
    public bool SearchFailed { get; private set; }
    public string? SearchClarificationMessage { get; private set; }
    public bool StationsListed { get; private set; }
    public TripSearchCriteria? LatestCriteria { get; private set; }

    public WaterbusBookingPlugin(
        ITripSearchTool tripSearchTool,
        IBookingLinkTool bookingLinkTool,
        int maxToolCalls = 5,
        TripSearchCriteria? previousSearch = null)
    {
        _tripSearchTool = tripSearchTool ?? throw new ArgumentNullException(nameof(tripSearchTool));
        _bookingLinkTool = bookingLinkTool ?? throw new ArgumentNullException(nameof(bookingLinkTool));
        _maxToolCalls = maxToolCalls > 0 ? maxToolCalls : throw new ArgumentOutOfRangeException(nameof(maxToolCalls));
        _previousSearch = previousSearch;
    }

    /// <summary>
    /// Danh sách các thẻ chuyến thật thu thập được từ các lần gọi tool tra cứu thực tế.
    /// </summary>
    public IReadOnlyList<AiTripCardDto> InterceptedCards => _interceptedCards.AsReadOnly();
    public IReadOnlyList<string> ListedStations => _listedStations.AsReadOnly();

    /// <summary>
    /// Danh sách các lần gọi tool đã thực hiện trong phiên làm việc.
    /// </summary>
    public IReadOnlyList<AiToolExecutionDto> ExecutedTools => _executedTools.AsReadOnly();

    [KernelFunction("ListStations")]
    [Description("Liệt kê các bến tàu đang hoạt động để khách biết có thể tìm chuyến từ/đến những bến nào. Chỉ gọi khi khách hỏi danh sách bến.")]
    public async Task<string> ListStationsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryStartToolCall()) return "Đã đạt giới hạn gọi công cụ trong lượt hội thoại này.";
        var stationNames = await _tripSearchTool.ListActiveStationsAsync(cancellationToken);
        StationsListed = true;
        _listedStations.AddRange(stationNames);
        _executedTools.Add(new AiToolExecutionDto("ListStations", "Danh sách bến đang hoạt động", true, null));
        return stationNames.Count == 0
            ? "Hiện chưa có bến tàu đang hoạt động."
            : "Các bến đang hoạt động: " + string.Join(", ", stationNames);
    }

    [KernelFunction("SearchTrips")]
    [Description("Tìm các chuyến tàu sắp khởi hành. Không nêu bến thì liệt kê chuyến sắp tới; một bến thì tìm cả chiều đi và đến; hai bến thì tìm đúng chiều. Có thể lọc giờ Việt Nam, ngày, số khách và gợi ý chuyến gần giờ mong muốn. Khi khách sửa tiêu chí cũ, đặt usePreviousSearch=true.")]
    public async Task<string> SearchTripsAsync(
        [Description("Tên bến đi hoặc bến duy nhất khách nhắc đến (ví dụ: 'Bến Bạch Đằng'); nếu chỉ một bến, hệ thống tìm cả chiều đi và đến.")] string? departureStation = null,
        [Description("Tên bến đến (ví dụ: 'Bến Linh Đông'); nếu chỉ một bến, hệ thống tìm cả chiều đi và đến.")] string? arrivalStation = null,
        [Description("Ngày khởi hành theo định dạng YYYY-MM-DD; bỏ trống để tìm các chuyến sắp tới.")] string? departureDate = null,
        [Description("Loại dịch vụ: 'Commuter' (tàu buýt thông thường) hoặc 'Sightseeing' (tàu ngắm cảnh).")] string? tripType = null,
        [Description("Số hành khách khi khách muốn lọc theo số ghế trống (1 đến 50); bỏ trống để xem mọi chuyến.")] int? passengerCount = null,
        [Description("Giờ khởi hành sớm nhất theo giờ Việt Nam HH:mm, ví dụ 'sau 4 giờ chiều' là 16:00.")] string? earliestDepartureTime = null,
        [Description("Giờ khởi hành muộn nhất theo giờ Việt Nam HH:mm.")] string? latestDepartureTime = null,
        [Description("Giờ khởi hành khách muốn đi theo giờ Việt Nam HH:mm; dùng để xếp chuyến gần giờ này lên đầu.")] string? preferredDepartureTime = null,
        [Description("Giờ đến muộn nhất theo giờ Việt Nam HH:mm, ví dụ 'đến trước 17 giờ' là 17:00.")] string? latestArrivalTime = null,
        [Description("True nếu khách muốn điều chỉnh lần tìm trước, ví dụ 'sớm hơn', 'đổi ngày', 'thêm một người'; false nếu là câu tìm mới.")] bool usePreviousSearch = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryStartToolCall()) return "Đã đạt giới hạn gọi công cụ trong lượt hội thoại này.";

        var previous = usePreviousSearch ? _previousSearch : null;
        DateOnly? parsedDate = previous?.DepartureDate;
        if (!string.IsNullOrWhiteSpace(departureDate))
        {
            if (!DateOnly.TryParseExact(departureDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                SearchNeedsClarification = true;
                SearchClarificationMessage = "Ngày khởi hành không hợp lệ. Vui lòng cho biết ngày theo định dạng YYYY-MM-DD.";
                return SearchClarificationMessage;
            }
            parsedDate = date;
        }

        var normalizedType = NormalizeTripType(tripType);
        normalizedType ??= string.IsNullOrWhiteSpace(tripType) ? previous?.TripType : null;
        if ((!string.IsNullOrWhiteSpace(tripType) && normalizedType == null)
            || (passengerCount.HasValue && (passengerCount < 1 || passengerCount > 50)))
        {
            SearchNeedsClarification = true;
            SearchClarificationMessage = "Loại chuyến hoặc số lượng hành khách không hợp lệ; vui lòng kiểm tra lại.";
            return SearchClarificationMessage;
        }

        if (!TryParseTime(earliestDepartureTime, previous?.EarliestDepartureTime, out var parsedEarliest)
            || !TryParseTime(latestDepartureTime, previous?.LatestDepartureTime, out var parsedLatest)
            || !TryParseTime(preferredDepartureTime, previous?.PreferredDepartureTime, out var parsedPreferred)
            || !TryParseTime(latestArrivalTime, previous?.LatestArrivalTime, out var parsedArrival))
        {
            SearchNeedsClarification = true;
            SearchClarificationMessage = "Giờ không hợp lệ. Vui lòng nêu giờ theo định dạng HH:mm.";
            return SearchClarificationMessage;
        }

        var criteria = new TripSearchCriteria
        {
            DepartureStationName = departureStation?.Trim() ?? previous?.DepartureStationName,
            ArrivalStationName = arrivalStation?.Trim() ?? previous?.ArrivalStationName,
            DepartureDate = parsedDate,
            TripType = normalizedType,
            PassengerCount = passengerCount ?? previous?.PassengerCount,
            EarliestDepartureTime = parsedEarliest,
            LatestDepartureTime = parsedLatest,
            PreferredDepartureTime = parsedPreferred,
            LatestArrivalTime = parsedArrival
        };

        var validation = await new TripSearchCriteriaValidator().ValidateAsync(criteria, cancellationToken);
        if (!validation.IsValid)
        {
            SearchNeedsClarification = true;
            SearchClarificationMessage = "Tiêu chí tìm chuyến không hợp lệ; vui lòng kiểm tra lại ngày, bến hoặc loại chuyến.";
            return SearchClarificationMessage;
        }

        SearchAttempted = true;
        var searchResult = await _tripSearchTool.SearchTripsAsync(criteria, cancellationToken);
        SearchNeedsClarification = searchResult.RequiresMoreClarification;
        SearchClarificationMessage = searchResult.RequiresMoreClarification ? searchResult.Message : null;
        SearchFailed = !searchResult.IsSuccess && !searchResult.RequiresMoreClarification;
        if (searchResult.IsSuccess && !searchResult.RequiresMoreClarification)
            LatestCriteria = criteria;

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
            return "Không tìm thấy chuyến tàu nào phù hợp trong dữ liệu Trip Search hiện tại.";
        }

        // Lưu thẻ chuyến thực tế để trả về cho UI
        foreach (var trip in searchResult.Trips)
        {
            if (!_interceptedCards.Any(c => c.TripId == trip.TripId))
            {
                // Search tool không có quyền cung cấp booking URL; chỉ preview Development được giữ giá chưa xác thực.
                _interceptedCards.Add(trip with
                {
                    BookingUrl = null,
                    BasePrice = trip.IsPriceVerified || trip.IsDevelopmentPreview ? trip.BasePrice : null
                });
            }
        }

        // Tạo tóm tắt văn bản trả về cho LLM tham khảo
        var summaryLines = searchResult.Trips.Select((t, index) =>
            $"{index + 1}. Tuyến '{t.RouteName}' (Mã: {t.TripId}): Khởi hành lúc {t.DepartureTime:HH:mm} tại {t.DepartureStationName}, đến {t.ArrivalStationName} lúc {t.ArrivalTime:HH:mm}. " +
            $"Còn {t.AvailableSeatsCount} ghế. Giá: {(t.IsPriceVerified && t.BasePrice.HasValue ? $"{t.BasePrice:N0} VNĐ" : "Chưa xác thực")}.");

        return $"Tìm thấy {searchResult.Trips.Count} chuyến tàu theo dữ liệu Trip Search:\n" + string.Join("\n", summaryLines);
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

    private static bool TryParseTime(string? value, TimeOnly? previous, out TimeOnly? parsed)
    {
        parsed = previous;
        if (value is null) return true;
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return false;
        parsed = time;
        return true;
    }

    private bool TryStartToolCall()
    {
        if (_toolCallCount >= _maxToolCalls) return false;
        _toolCallCount++;
        return true;
    }
}
