using FluentValidation;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Features.AiBooking.Validators;

namespace WaterbusSystem.UnitTests.AiBooking.Fakes;

/// <summary>
/// Fake adapter của ITripSearchTool phục vụ kiểm thử đơn vị và phân định ranh giới độc lập.
/// Giả lập hành vi truy vấn API/Query tìm chuyến có cấu trúc:
/// - Khi thiếu thông tin bến: yêu cầu làm rõ (RequiresMoreClarification = true), không tự đoán bến.
/// - Khi không có chuyến: trả về danh sách rỗng, không tự bịa chuyến.
/// - Không tự bịa giá khi pricing chưa xác thực.
/// </summary>
public class FakeTripSearchTool : ITripSearchTool
{
    private readonly List<AiTripCardDto> _availableTrips = new();
    private readonly bool _simulateEmptyResult;
    private readonly IValidator<TripSearchCriteria> _validator = new TripSearchCriteriaValidator();

    public FakeTripSearchTool(IEnumerable<AiTripCardDto>? initialTrips = null, bool simulateEmptyResult = false)
    {
        _simulateEmptyResult = simulateEmptyResult;

        if (initialTrips != null)
        {
            _availableTrips.AddRange(initialTrips);
        }
        else if (!simulateEmptyResult)
        {
            // Mẫu chuyến mặc định
            var bachDangId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var linhDongId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var departure = new DateTimeOffset(
                DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(8),
                TimeSpan.FromHours(7));

            _availableTrips.Add(new AiTripCardDto(
                TripId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                RouteName: "Bạch Đằng - Linh Đông",
                DepartureStationName: "Bến Bạch Đằng",
                ArrivalStationName: "Bến Linh Đông",
                DepartureTime: departure,
                ArrivalTime: departure.AddMinutes(45),
                DurationMinutes: 45,
                TripType: "Commuter",
                BasePrice: null, // Chưa xác thực bởi FareRule, không bịa giá
                IsPriceVerified: false,
                AvailableSeatsCount: 25,
                BookingUrl: null,
                Stops: new List<AiTripStopDto>
                {
                    new(bachDangId, "Bến Bạch Đằng", 1),
                    new(Guid.NewGuid(), "Bến Bình An", 2),
                    new(linhDongId, "Bến Linh Đông", 3)
                }));
        }
    }

    public async Task<TripSearchResult> SearchTripsAsync(
        TripSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await _validator.ValidateAsync(criteria, cancellationToken);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Chỉ tra cứu khi có đủ hai bến và ngày; fake không được tự chọn mặc định.
        if ((string.IsNullOrWhiteSpace(criteria.DepartureStationName) && !criteria.DepartureStationId.HasValue) ||
            (string.IsNullOrWhiteSpace(criteria.ArrivalStationName) && !criteria.ArrivalStationId.HasValue) ||
            !criteria.DepartureDate.HasValue)
        {
            return new TripSearchResult(
                Trips: Array.Empty<AiTripCardDto>(),
                IsSuccess: true,
                Message: "Vui lòng cho biết bến khởi hành, bến đến và ngày đi.",
                RequiresMoreClarification: true);
        }

        if (_simulateEmptyResult)
        {
            return new TripSearchResult(
                Trips: Array.Empty<AiTripCardDto>(),
                IsSuccess: true,
                Message: "Không tìm thấy chuyến tàu nào phù hợp.",
                RequiresMoreClarification: false);
        }

        var filtered = _availableTrips.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(criteria.DepartureStationName))
        {
            filtered = filtered.Where(t => t.DepartureStationName.Contains(criteria.DepartureStationName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.ArrivalStationName))
        {
            filtered = filtered.Where(t => t.ArrivalStationName.Contains(criteria.ArrivalStationName, StringComparison.OrdinalIgnoreCase));
        }

        if (criteria.DepartureStationId.HasValue)
            filtered = filtered.Where(t => t.Stops.Count > 0 && t.Stops[0].StationId == criteria.DepartureStationId.Value);

        if (criteria.ArrivalStationId.HasValue)
            filtered = filtered.Where(t => t.Stops.Count > 0 && t.Stops[^1].StationId == criteria.ArrivalStationId.Value);

        filtered = filtered.Where(t => DateOnly.FromDateTime(t.DepartureTime.ToOffset(TimeSpan.FromHours(7)).DateTime) == criteria.DepartureDate.Value);

        if (!string.IsNullOrWhiteSpace(criteria.TripType))
        {
            filtered = filtered.Where(t => string.Equals(t.TripType, criteria.TripType, StringComparison.OrdinalIgnoreCase));
        }

        var resultList = filtered.ToList();

        return new TripSearchResult(
            Trips: resultList,
            IsSuccess: true,
            Message: resultList.Count > 0 ? $"Tìm thấy {resultList.Count} chuyến phù hợp." : "Không tìm thấy chuyến tàu nào.",
            RequiresMoreClarification: false);
    }
}
