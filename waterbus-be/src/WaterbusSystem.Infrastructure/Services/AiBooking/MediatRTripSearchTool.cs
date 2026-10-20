using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Features.Stations.Queries.GetStations;
using WaterbusSystem.Application.Features.Trips.Queries.SearchTrips;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Infrastructure.Services.AiBooking;

/// <summary>
/// Adapter AI dùng cùng query với GET /api/v1/trips/search; không sửa Trip Search của teammate.
/// Chỉ phát Trip Card sau khi owner xác nhận contract bến/giá và kết quả qua kiểm tra an toàn.
/// </summary>
public sealed class MediatRTripSearchTool : ITripSearchTool
{
    private readonly ISender _sender;
    private readonly IOptions<AiBookingIntegrationOptions> _options;
    private readonly IHostEnvironment _environment;

    public MediatRTripSearchTool(
        ISender sender, IOptions<AiBookingIntegrationOptions> options, IHostEnvironment environment)
    {
        _sender = sender;
        _options = options;
        _environment = environment;
    }

    public async Task<IReadOnlyList<string>> ListActiveStationsAsync(CancellationToken cancellationToken = default)
    {
        var stations = await _sender.Send(new GetStationsQuery(), cancellationToken);
        return stations.Select(s => s.Name).ToList();
    }

    public async Task<TripSearchResult> SearchTripsAsync(
        TripSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var hasDeparture = criteria.DepartureStationId.HasValue || !string.IsNullOrWhiteSpace(criteria.DepartureStationName);
        var hasArrival = criteria.ArrivalStationId.HasValue || !string.IsNullOrWhiteSpace(criteria.ArrivalStationName);

        var developmentPreview = _environment.IsDevelopment()
            && _options.Value.EnableUnverifiedTripCardsForDevelopment
            && !_options.Value.IsTripSearchContractVerified;
        if (!_options.Value.IsTripSearchContractVerified && !developmentPreview)
            return new([], false, "Hợp đồng Trip Search chưa được xác nhận.");

        var stations = await _sender.Send(new GetStationsQuery(), cancellationToken);
        var departureId = hasDeparture
            ? ResolveStationId(stations, criteria.DepartureStationId, criteria.DepartureStationName) : null;
        var arrivalId = hasArrival
            ? ResolveStationId(stations, criteria.ArrivalStationId, criteria.ArrivalStationName) : null;
        if ((hasDeparture && !departureId.HasValue) || (hasArrival && !arrivalId.HasValue)
            || (departureId.HasValue && departureId == arrivalId))
        {
            var unknownName = !departureId.HasValue && hasDeparture
                ? criteria.DepartureStationName : criteria.ArrivalStationName;
            var suggestions = FindStationSuggestions(stations, unknownName);
            var message = departureId.HasValue && departureId == arrivalId
                ? "Bến đi và bến đến phải khác nhau."
                : suggestions.Count > 0
                    ? $"Khu vực '{unknownName}' có thể là: {string.Join(", ", suggestions)}. Vui lòng chọn một bến cụ thể."
                    : $"Chưa xác định được bến '{unknownName}'. Vui lòng chọn tên bến cụ thể trong danh sách bến đang hoạt động.";
            return new([], true, message, true);
        }

        TripType? tripType = null;
        if (!string.IsNullOrWhiteSpace(criteria.TripType))
        {
            if (!Enum.TryParse(criteria.TripType, true, out TripType parsedType)
                || !Enum.IsDefined(parsedType))
                return new([], true, "Loại chuyến không hợp lệ.", true);
            tripType = parsedType;
        }

        var date = criteria.DepartureDate?.ToDateTime(TimeOnly.MinValue);
        List<TripDto> trips;
        if (hasDeparture != hasArrival)
        {
            // Một bến không xác định chiều: tìm cả chuyến xuất phát và chuyến đến bến đó.
            var stationId = departureId ?? arrivalId;
            var departing = await _sender.Send(new SearchTripsQuery(stationId, null, date, tripType), cancellationToken);
            var arriving = await _sender.Send(new SearchTripsQuery(null, stationId, date, tripType), cancellationToken);
            trips = departing.Concat(arriving).DistinctBy(t => t.Id).OrderBy(t => t.DepartureTime).ToList();
        }
        else
        {
            // Không nêu bến: lấy danh sách chuyến sắp tới; đủ hai bến: tìm đúng chiều khách yêu cầu.
            trips = await _sender.Send(new SearchTripsQuery(departureId, arrivalId, date, tripType), cancellationToken);
        }

        // Query gốc chưa loại chuyến đã qua giờ; AI chỉ giới thiệu những chuyến còn ở phía trước.
        var upcomingTrips = trips.Where(t => t.DepartureTime > DateTimeOffset.UtcNow)
            .Where(t => !criteria.EarliestDepartureTime.HasValue
                || LocalTime(t.DepartureTime) >= criteria.EarliestDepartureTime.Value)
            .Where(t => !criteria.LatestDepartureTime.HasValue
                || LocalTime(t.DepartureTime) <= criteria.LatestDepartureTime.Value)
            .Where(t => !criteria.LatestArrivalTime.HasValue
                || (LocalDate(t.DepartureTime) == LocalDate(t.ArrivalTime)
                    && LocalTime(t.ArrivalTime) <= criteria.LatestArrivalTime.Value))
            .ToList();

        // Không đưa kết quả sai/thiếu giá hoặc bến cho AI, kể cả khi cờ tích hợp bật nhầm.
        if (upcomingTrips.Any(t => t.BasePrice < 0 || (t.BasePrice == 0 && !developmentPreview)
            || t.Stops is null
            || (departureId.HasValue && !t.Stops.Any(s => s.StationId == departureId))
            || (arrivalId.HasValue && !t.Stops.Any(s => s.StationId == arrivalId))))
            return new([], false, "Dữ liệu giá hoặc bến của Trip Search chưa đáng tin.");

        var matchingTrips = upcomingTrips
            .Where(t => !criteria.PassengerCount.HasValue || t.AvailableSeatsCount >= criteria.PassengerCount.Value)
            .OrderBy(t => criteria.PreferredDepartureTime.HasValue
                ? Math.Abs((LocalTime(t.DepartureTime).ToTimeSpan() - criteria.PreferredDepartureTime.Value.ToTimeSpan()).TotalMinutes)
                : 0)
            .ThenBy(t => t.DepartureTime)
            .Take(criteria.PreferredDepartureTime.HasValue ? 3 : 10);
        var cards = matchingTrips
            .Select(t =>
            {
                // Một bến có thể là bến đi hoặc bến đến; ID trên card luôn theo chiều của chuyến thực tế.
                var boardingId = ResolveStationId(stations, null, t.DepartureStationName);
                var disembarkingId = ResolveStationId(stations, null, t.ArrivalStationName);
                return new AiTripCardDto(
                    t.Id, t.RouteName, t.DepartureStationName, t.ArrivalStationName,
                    t.DepartureTime, t.ArrivalTime, t.DurationMinutes, t.TripType,
                    t.BasePrice, !developmentPreview, t.AvailableSeatsCount, null,
                    t.Stops.Select(s => new AiTripStopDto(s.StationId, s.StationName, s.OrderIndex)).ToList(),
                    boardingId, disembarkingId, developmentPreview);
            })
            .ToList();

        return new(cards, true);
    }

    private static Guid? ResolveStationId(
        IReadOnlyList<StationDto> stations, Guid? id, string? name)
    {
        if (id.HasValue)
            return stations.Any(s => s.Id == id.Value) ? id : null;
        var matches = stations.Where(s => string.Equals(
            NormalizeStationName(s.Name), NormalizeStationName(name), StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }

    private static string? NormalizeStationName(string? name)
    {
        var normalized = name?.Trim();
        if (normalized?.StartsWith("Bến ", StringComparison.OrdinalIgnoreCase) == true)
            normalized = normalized[4..].Trim();
        return normalized;
    }

    private static IReadOnlyList<string> FindStationSuggestions(IReadOnlyList<StationDto> stations, string? keyword)
    {
        var normalized = RemoveDiacritics(NormalizeStationName(keyword));
        foreach (var prefix in new[] { "khu vuc ", "quan ", "thanh pho ", "tp ", "o gan " })
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..].Trim();
                break;
            }
        }
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 3)
            return [];
        return stations.Where(s => RemoveDiacritics($"{s.Name} {s.Address}").Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Take(5).Select(s => s.Name).ToList();
    }

    private static string RemoveDiacritics(string? value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character is 'đ' or 'Đ' ? 'd' : character);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static TimeOnly LocalTime(DateTimeOffset value) => TimeOnly.FromTimeSpan(value.ToOffset(TimeSpan.FromHours(7)).TimeOfDay);
    private static DateOnly LocalDate(DateTimeOffset value) => DateOnly.FromDateTime(value.ToOffset(TimeSpan.FromHours(7)).DateTime);
}
