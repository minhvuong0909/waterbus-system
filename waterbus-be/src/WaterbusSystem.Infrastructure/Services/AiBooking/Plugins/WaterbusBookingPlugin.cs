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
    private readonly bool _english;
    private readonly DateOnly? _requiredDate;
    private readonly TimeOnly? _requiredEarliestTime;
    private readonly TimeOnly? _requiredLatestTime;
    private readonly bool _nearestDeparturesOnly;
    private readonly bool _explicitArrivalOnly;
    private readonly bool _explicitDepartureOnly;
    private readonly bool _forcePreviousSearch;
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
        TripSearchCriteria? previousSearch = null,
        string? languageCode = null,
        DateOnly? requiredDate = null,
        TimeOnly? requiredEarliestTime = null,
        TimeOnly? requiredLatestTime = null,
        bool nearestDeparturesOnly = false,
        bool explicitArrivalOnly = false,
        bool explicitDepartureOnly = false,
        bool forcePreviousSearch = false)
    {
        _tripSearchTool = tripSearchTool ?? throw new ArgumentNullException(nameof(tripSearchTool));
        _bookingLinkTool = bookingLinkTool ?? throw new ArgumentNullException(nameof(bookingLinkTool));
        _maxToolCalls = maxToolCalls > 0 ? maxToolCalls : throw new ArgumentOutOfRangeException(nameof(maxToolCalls));
        _previousSearch = previousSearch;
        _english = languageCode == "en";
        _requiredDate = requiredDate;
        _requiredEarliestTime = requiredEarliestTime;
        _requiredLatestTime = requiredLatestTime;
        _nearestDeparturesOnly = nearestDeparturesOnly;
        _explicitArrivalOnly = explicitArrivalOnly;
        _explicitDepartureOnly = explicitDepartureOnly;
        _forcePreviousSearch = forcePreviousSearch;
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
    [Description("List active piers / Liệt kê bến đang hoạt động. Call when the passenger asks which piers exist / khách hỏi danh sách bến.")]
    public async Task<string> ListStationsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryStartToolCall()) return Text("Tool-call limit reached for this message.", "Đã đạt giới hạn gọi công cụ trong lượt hội thoại này.");
        var stationNames = await _tripSearchTool.ListActiveStationsAsync(cancellationToken);
        StationsListed = true;
        _listedStations.AddRange(stationNames);
        _executedTools.Add(new AiToolExecutionDto("ListStations", "Danh sách bến đang hoạt động", true, null));
        return stationNames.Count == 0
            ? Text("There are no active piers at the moment.", "Hiện chưa có bến tàu đang hoạt động.")
            : Text("Active piers: ", "Các bến đang hoạt động: ") + string.Join(", ", stationNames);
    }

    [KernelFunction("SearchTrips")]
    [Description("Search upcoming trips by pier, date, Vietnam local time, API fare and available seats / tìm chuyến sắp khởi hành. No pier means all upcoming trips. An unspecified single pier searches both directions; 'destination/điểm đến/bến đến X' means arrivalStation=X only, 'origin/bến đi/từ X' means departureStation=X only. usePreviousSearch=true only for a follow-up changing prior criteria; all trips/new search means false.")]
    public async Task<string> SearchTripsAsync(
        [Description("Departure pier name, or the only pier if its direction is unspecified / bến đi hoặc bến duy nhất chưa rõ chiều. Do NOT use for 'điểm đến X'. Example: Bến Bạch Đằng.")] string? departureStation = null,
        [Description("Arrival pier name / bến đến. Example: Bến Linh Đông.")] string? arrivalStation = null,
        [Description("Departure date YYYY-MM-DD / ngày đi; omit for all upcoming trips.")] string? departureDate = null,
        [Description("Loại dịch vụ: 'Commuter' (tàu buýt thông thường) hoặc 'Sightseeing' (tàu ngắm cảnh).")] string? tripType = null,
        [Description("Số hành khách khi khách muốn lọc theo số ghế trống (1 đến 50); bỏ trống để xem mọi chuyến.")] int? passengerCount = null,
        [Description("Giờ khởi hành sớm nhất theo giờ Việt Nam HH:mm, ví dụ 'sau 4 giờ chiều' là 16:00.")] string? earliestDepartureTime = null,
        [Description("Giờ khởi hành muộn nhất theo giờ Việt Nam HH:mm.")] string? latestDepartureTime = null,
        [Description("Giờ khởi hành khách muốn đi theo giờ Việt Nam HH:mm; dùng để xếp chuyến gần giờ này lên đầu.")] string? preferredDepartureTime = null,
        [Description("Giờ đến muộn nhất theo giờ Việt Nam HH:mm, ví dụ 'đến trước 17 giờ' là 17:00.")] string? latestArrivalTime = null,
        [Description("True for a follow-up ('earlier', 'tomorrow', 'one more passenger') / sửa lần tìm trước; false for a new/all-trips search.")] bool usePreviousSearch = false,
        [Description("API fare strictly above this VND amount / giá trên mức này. 'Over 50k' = 50000.")] decimal? priceGreaterThan = null,
        [Description("API fare strictly below this VND amount / giá dưới mức này. 'Under 50k' = 50000.")] decimal? priceLessThan = null,
        [Description("Minimum available seats / ghế trống tối thiểu. 'Trips with seats left' = 1.")] int? minimumAvailableSeats = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryStartToolCall()) return Text("Tool-call limit reached for this message.", "Đã đạt giới hạn gọi công cụ trong lượt hội thoại này.");

        var previous = usePreviousSearch || _forcePreviousSearch ? _previousSearch : null;
        // Câu hỏi nêu buổi mới không được mang giờ lọc cũ sang, nhưng có thể giữ bến của lượt trước.
        if (previous is not null && (_requiredEarliestTime.HasValue || _requiredLatestTime.HasValue))
            previous = previous with
            {
                EarliestDepartureTime = null,
                LatestDepartureTime = null,
                PreferredDepartureTime = null,
                LatestArrivalTime = null
            };
        DateOnly? parsedDate = previous?.DepartureDate;
        if (!_requiredDate.HasValue && !string.IsNullOrWhiteSpace(departureDate))
        {
            if (!DateOnly.TryParseExact(departureDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                SearchNeedsClarification = true;
                SearchClarificationMessage = Text("Invalid departure date. Please use YYYY-MM-DD.",
                    "Ngày khởi hành không hợp lệ. Vui lòng cho biết ngày theo định dạng YYYY-MM-DD.");
                return SearchClarificationMessage;
            }
            parsedDate = date;
        }
        parsedDate = _requiredDate ?? parsedDate;

        var normalizedType = NormalizeTripType(tripType);
        normalizedType ??= string.IsNullOrWhiteSpace(tripType) ? previous?.TripType : null;
        if ((!string.IsNullOrWhiteSpace(tripType) && normalizedType == null)
            || (passengerCount.HasValue && (passengerCount < 1 || passengerCount > 50)))
        {
            SearchNeedsClarification = true;
            SearchClarificationMessage = Text("Invalid trip type or passenger count. Please check your request.",
                "Loại chuyến hoặc số lượng hành khách không hợp lệ; vui lòng kiểm tra lại.");
            return SearchClarificationMessage;
        }

        if (!TryParseTime(earliestDepartureTime, previous?.EarliestDepartureTime, out var parsedEarliest)
            || !TryParseTime(latestDepartureTime, previous?.LatestDepartureTime, out var parsedLatest)
            || !TryParseTime(preferredDepartureTime, previous?.PreferredDepartureTime, out var parsedPreferred)
            || !TryParseTime(latestArrivalTime, previous?.LatestArrivalTime, out var parsedArrival))
        {
            SearchNeedsClarification = true;
            SearchClarificationMessage = Text("Invalid time. Please use HH:mm.",
                "Giờ không hợp lệ. Vui lòng nêu giờ theo định dạng HH:mm.");
            return SearchClarificationMessage;
        }
        if (_requiredEarliestTime.HasValue && (!parsedEarliest.HasValue || parsedEarliest < _requiredEarliestTime))
            parsedEarliest = _requiredEarliestTime;
        if (_requiredLatestTime.HasValue && (!parsedLatest.HasValue || parsedLatest > _requiredLatestTime))
            parsedLatest = _requiredLatestTime;

        // Model có thể điền bến duy nhất vào sai tham số. Chiều nói rõ trong câu của khách
        // luôn thắng: 'điểm đến Bạch Đằng' không được biến thành tìm hai chiều.
        if (_explicitArrivalOnly && !string.IsNullOrWhiteSpace(departureStation)
            && string.IsNullOrWhiteSpace(arrivalStation))
        {
            arrivalStation = departureStation;
            departureStation = null;
        }
        else if (_explicitDepartureOnly && !string.IsNullOrWhiteSpace(arrivalStation)
            && string.IsNullOrWhiteSpace(departureStation))
        {
            departureStation = arrivalStation;
            arrivalStation = null;
        }

        var criteria = new TripSearchCriteria
        {
            DepartureStationName = departureStation?.Trim() ?? previous?.DepartureStationName,
            ArrivalStationName = arrivalStation?.Trim() ?? previous?.ArrivalStationName,
            RestrictSingleStationToRequestedDirection = _explicitArrivalOnly || _explicitDepartureOnly
                || previous?.RestrictSingleStationToRequestedDirection == true,
            DepartureDate = parsedDate,
            TripType = normalizedType,
            PassengerCount = passengerCount ?? previous?.PassengerCount,
            MinimumAvailableSeats = minimumAvailableSeats ?? previous?.MinimumAvailableSeats,
            PriceGreaterThan = priceGreaterThan ?? previous?.PriceGreaterThan,
            PriceLessThan = priceLessThan ?? previous?.PriceLessThan,
            EarliestDepartureTime = parsedEarliest,
            LatestDepartureTime = parsedLatest,
            PreferredDepartureTime = parsedPreferred,
            LatestArrivalTime = parsedArrival
        };

        var validation = await new TripSearchCriteriaValidator().ValidateAsync(criteria, cancellationToken);
        if (!validation.IsValid)
        {
            SearchNeedsClarification = true;
            SearchClarificationMessage = Text("Invalid search criteria. Please check the date, pier, trip type, fare or seats.",
                "Tiêu chí tìm chuyến không hợp lệ; vui lòng kiểm tra lại ngày, bến hoặc loại chuyến.");
            return SearchClarificationMessage;
        }

        SearchAttempted = true;
        _interceptedCards.Clear();
        var searchResult = await _tripSearchTool.SearchTripsAsync(criteria, cancellationToken);
        SearchNeedsClarification = searchResult.RequiresMoreClarification;
        SearchClarificationMessage = searchResult.RequiresMoreClarification
            ? (_english ? "Please choose a specific active pier or check the trip type." : searchResult.Message)
            : null;
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
            return SearchClarificationMessage ?? Text("Please clarify your trip request.", "Cần thêm thông tin để tra cứu chuyến chính xác.");
        }

        if (!searchResult.IsSuccess)
            return Text("Trip search is temporarily unavailable. Please try again.",
                "Hệ thống tìm chuyến đang gặp lỗi; vui lòng thử lại sau.");

        if (searchResult.Trips.Count == 0)
        {
            return Text("No matching trips were found.",
                "Không tìm thấy chuyến tàu nào phù hợp.");
        }

        // "Sắp khởi hành" = những chuyến gần nhất trong ngày phục vụ gần nhất, không phải
        // tất cả chuyến của nhiều ngày. Tool nguồn vẫn là Trip Search thật.
        var displayTrips = searchResult.Trips.OrderBy(t => t.DepartureTime).ToList();
        if (_nearestDeparturesOnly && displayTrips.Count > 0)
        {
            var nearestDate = DateOnly.FromDateTime(
                displayTrips[0].DepartureTime.ToOffset(TimeSpan.FromHours(7)).DateTime);
            displayTrips = displayTrips
                .Where(t => DateOnly.FromDateTime(t.DepartureTime.ToOffset(TimeSpan.FromHours(7)).DateTime) == nearestDate)
                .Take(3).ToList();
        }

        // Chỉ giữ thẻ của lần tìm mới nhất trong cùng lượt; không cộng dồn các lần lọc.
        foreach (var trip in displayTrips)
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
        var culture = CultureInfo.GetCultureInfo(_english ? "en-US" : "vi-VN");
        var summaryLines = displayTrips.Select((t, index) => _english
            ? $"{index + 1}. {t.RouteName} ({t.TripId}): {t.DepartureStationName} → {t.ArrivalStationName}, {t.DepartureTime:HH:mm}; {t.AvailableSeatsCount} seats left; fare: {(t.BasePrice.HasValue ? t.BasePrice.Value.ToString("N0", culture) + " VND" : "unavailable")}."
            : $"{index + 1}. Tuyến '{t.RouteName}' (Mã: {t.TripId}): Khởi hành lúc {t.DepartureTime:HH:mm} tại {t.DepartureStationName}, đến {t.ArrivalStationName} lúc {t.ArrivalTime:HH:mm}. Còn {t.AvailableSeatsCount} ghế. Giá vé: {(t.BasePrice.HasValue ? t.BasePrice.Value.ToString("N0", culture) + " VNĐ" : "chưa có dữ liệu")}.");

        return Text($"Found {displayTrips.Count} matching trips:\n",
            $"Tìm thấy {displayTrips.Count} chuyến phù hợp:\n") + string.Join("\n", summaryLines);
    }

    [KernelFunction("BuildBookingLink")]
    [Description("Build an official booking link for a searched trip / tạo đường dẫn đặt vé chính thức theo TripId.")]
    public async Task<string> BuildBookingLinkAsync(
        [Description("Mã định danh duy nhất của chuyến tàu (TripId dạng GUID). Bắt buộc.")] string tripId,
        [Description("Mã định danh bến lên tàu (GUID, tùy chọn).")] string? boardingStationId = null,
        [Description("Mã định danh bến xuống tàu (GUID, tùy chọn).")] string? disembarkingStationId = null,
        [Description("Số lượng hành khách (1 đến 50, tùy chọn).")] int? passengerCount = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryStartToolCall()) return Text("Tool-call limit reached for this message.", "Đã đạt giới hạn gọi công cụ trong lượt hội thoại này.");

        if (!Guid.TryParse(tripId, out var parsedTripId) || parsedTripId == Guid.Empty)
        {
            return Text("Invalid TripId.", "Mã chuyến tàu (TripId) không hợp lệ.");
        }

        var cardIndex = _interceptedCards.FindIndex(c => c.TripId == parsedTripId);
        if (cardIndex < 0)
            return Text("I can only build a link for a trip returned by the current search.",
                "Chỉ có thể tạo link cho chuyến vừa được công cụ tìm kiếm trả về.");

        if ((!string.IsNullOrWhiteSpace(boardingStationId) && !Guid.TryParse(boardingStationId, out _))
            || (!string.IsNullOrWhiteSpace(disembarkingStationId) && !Guid.TryParse(disembarkingStationId, out _))
            || (passengerCount.HasValue && (passengerCount < 1 || passengerCount > 50)))
            return Text("Invalid boarding pier, arrival pier or passenger count.",
                "Tham số bến lên, bến xuống hoặc số hành khách không hợp lệ.");

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
            return Text("Invalid booking-link parameters.", "Tham số đặt vé không hợp lệ.");

        var linkResult = await _bookingLinkTool.BuildBookingLinkAsync(criteria, cancellationToken);

        _executedTools.Add(new AiToolExecutionDto(
            ToolName: "BuildBookingLink",
            ArgumentsSummary: "Chuyến đã có trong kết quả tìm kiếm",
            Success: linkResult.IsValid,
            ExecutionNote: null));

        if (!linkResult.IsValid || string.IsNullOrWhiteSpace(linkResult.BookingUrl))
        {
            return Text("A booking link is not available yet. Please try again later.",
                "Chưa thể tạo liên kết đặt vé; vui lòng thử lại sau.");
        }

        if (!Uri.TryCreate(linkResult.BookingUrl, UriKind.Absolute, out var bookingUri)
            || bookingUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(bookingUri.UserInfo))
            return Text("Invalid booking link.", "Liên kết đặt vé không hợp lệ.");

        // Cập nhật URL đặt vé vào thẻ chuyến tương ứng
        _interceptedCards[cardIndex] = _interceptedCards[cardIndex] with { BookingUrl = linkResult.BookingUrl };

        return Text($"Official booking link: {linkResult.BookingUrl}",
            $"Đã tạo liên kết đặt vé chính thức: {linkResult.BookingUrl}");
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

    private string Text(string english, string vietnamese) => _english ? english : vietnamese;
}
