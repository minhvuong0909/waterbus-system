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

    public async Task<TripSearchResult> SearchTripsAsync(
        TripSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (!criteria.DepartureDate.HasValue
            || (criteria.DepartureStationId is null && string.IsNullOrWhiteSpace(criteria.DepartureStationName))
            || (criteria.ArrivalStationId is null && string.IsNullOrWhiteSpace(criteria.ArrivalStationName)))
            return new([], true, "Cần bến đi, bến đến và ngày đi.", true);

        var developmentPreview = _environment.IsDevelopment()
            && _options.Value.EnableUnverifiedTripCardsForDevelopment
            && !_options.Value.IsTripSearchContractVerified;
        if (!_options.Value.IsTripSearchContractVerified && !developmentPreview)
            return new([], false, "Hợp đồng Trip Search chưa được xác nhận.");

        var stations = await _sender.Send(new GetStationsQuery(), cancellationToken);
        var departureId = ResolveStationId(stations, criteria.DepartureStationId, criteria.DepartureStationName);
        var arrivalId = ResolveStationId(stations, criteria.ArrivalStationId, criteria.ArrivalStationName);
        if (!departureId.HasValue || !arrivalId.HasValue || departureId == arrivalId)
            return new([], true, "Bến đi hoặc bến đến không rõ; vui lòng chọn bến hợp lệ.", true);

        TripType? tripType = null;
        if (!string.IsNullOrWhiteSpace(criteria.TripType))
        {
            if (!Enum.TryParse(criteria.TripType, true, out TripType parsedType)
                || !Enum.IsDefined(parsedType))
                return new([], true, "Loại chuyến không hợp lệ.", true);
            tripType = parsedType;
        }

        var trips = await _sender.Send(new SearchTripsQuery(
            departureId, arrivalId,
            criteria.DepartureDate.Value.ToDateTime(TimeOnly.MinValue), tripType), cancellationToken);

        // Không đưa kết quả sai/thiếu giá hoặc bến cho AI, kể cả khi cờ tích hợp bật nhầm.
        if (trips.Any(t => t.BasePrice < 0 || (t.BasePrice == 0 && !developmentPreview)
            || t.Stops is null
            || !t.Stops.Any(s => s.StationId == departureId)
            || !t.Stops.Any(s => s.StationId == arrivalId)))
            return new([], false, "Dữ liệu giá hoặc bến của Trip Search chưa đáng tin.");

        var cards = trips
            .Where(t => t.AvailableSeatsCount >= (criteria.PassengerCount ?? 1))
            .Take(10)
            .Select(t => new AiTripCardDto(
                t.Id, t.RouteName, t.DepartureStationName, t.ArrivalStationName,
                t.DepartureTime, t.ArrivalTime, t.DurationMinutes, t.TripType,
                t.BasePrice, !developmentPreview, t.AvailableSeatsCount, null,
                t.Stops.Select(s => new AiTripStopDto(s.StationId, s.StationName, s.OrderIndex)).ToList(),
                departureId, arrivalId, developmentPreview))
            .ToList();

        return new(cards, true);
    }

    private static Guid? ResolveStationId(
        IReadOnlyList<StationDto> stations, Guid? id, string? name)
    {
        if (id.HasValue)
            return stations.Any(s => s.Id == id.Value) ? id : null;
        var matches = stations.Where(s => string.Equals(
            s.Name.Trim(), name?.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }
}
