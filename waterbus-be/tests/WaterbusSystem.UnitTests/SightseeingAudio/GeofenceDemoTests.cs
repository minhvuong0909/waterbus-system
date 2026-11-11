using FluentAssertions;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Services.SightseeingAudio;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.UnitTests.SightseeingAudio.Fakes;
using Xunit;
using Xunit.Abstractions;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

/// <summary>Demo dễ đọc cho geofence thật; GPS/POI và player ACK đều là dữ liệu thử.</summary>
public sealed class GeofenceDemoTests(ITestOutputHelper output)
{
    [Fact]
    public async Task FourGpsSituations_ShowDistanceDecisionAndReason()
    {
        const decimal poiLat = 10.774968m;
        const decimal poiLng = 106.704710m;
        const int radiusMeters = 100;

        var boatId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var poiId = Guid.NewGuid();
        var distanceCalculator = new NtsGeoDistanceCalculator();
        var fakePlayer = new RecordingOnboardAudioTriggerDispatcher();
        var service = new PoiAudioTriggerDecisionService(
            distanceCalculator,
            new InMemoryPoiTriggerDeduplicationService(),
            fakePlayer);

        var context = new TripSightseeingContext
        {
            TripId = tripId,
            BoatId = boatId,
            RouteId = routeId,
            TripType = TripType.Sightseeing,
            Status = TripStatus.EnRoute
        };
        var poi = new PoiGeofenceCandidate
        {
            RoutePoiId = Guid.NewGuid(),
            PoiId = poiId,
            PoiName = "Bến Bạch Đằng",
            RouteId = routeId,
            Latitude = poiLat,
            Longitude = poiLng,
            TriggerRadiusM = radiusMeters,
            DisplayOrder = 1,
            IsPoiActive = true,
            IsRoutePoiActive = true,
            AudioGuides =
            [
                new AudioGuideCandidate
                {
                    AudioGuideId = Guid.NewGuid(),
                    PoiId = poiId,
                    LanguageCode = "vi",
                    IsPublished = true,
                    // URI mẫu chỉ là metadata; demo không gọi Azure Blob.
                    AudioAssetUri = "https://example.blob.core.windows.net/private/demo.mp3"
                }
            ]
        };

        BoatPositionSnapshot Position(decimal latitude) => new()
        {
            BoatId = boatId,
            TripId = tripId,
            Latitude = latitude,
            Longitude = poiLng,
            ObservedAt = DateTimeOffset.UtcNow,
            Source = "GPS"
        };

        // Sai khác vĩ độ tương ứng xấp xỉ 180 m và 80 m tại POI này.
        var outsidePosition = Position(poiLat + 0.00162m);
        var insidePosition = Position(poiLat + 0.00072m);
        var outsideDistance = distanceCalculator.CalculateDistanceMeters(
            outsidePosition.Latitude, outsidePosition.Longitude, poiLat, poiLng);
        var insideDistance = distanceCalculator.CalculateDistanceMeters(
            insidePosition.Latitude, insidePosition.Longitude, poiLat, poiLng);
        outsideDistance.Should().BeInRange(170, 190);
        insideDistance.Should().BeInRange(70, 90);

        output.WriteLine("=== GEOFENCE DEMO — DỮ LIỆU MẪU, KHÔNG GỌI AZURE/DB/LOA TÀU ===");
        output.WriteLine($"POI: {poi.PoiName} ({poiLat}, {poiLng}); bán kính: {radiusMeters} m");
        output.WriteLine("Player giả chỉ ACK đã nhận lệnh; KHÔNG chứng minh âm thanh phát ra loa.");
        output.WriteLine("Tình huống | Khoảng cách thực đo | Quyết định | Lý do");

        var outside = await service.EvaluateAndTriggerAsync(outsidePosition, context, [poi]);
        outside.Reason.Should().Be(TriggerDecisionReason.OutsideTriggerRadius);
        fakePlayer.DispatchedCount.Should().Be(0);
        Print("Ngoài vùng", outsideDistance, outside);

        var inside = await service.EvaluateAndTriggerAsync(insidePosition, context, [poi]);
        inside.Reason.Should().Be(TriggerDecisionReason.Triggered);
        inside.HasOnboardPlaybackAcknowledged.Should().BeFalse();
        fakePlayer.DispatchedCount.Should().Be(1);
        Print("Vào vùng", insideDistance, inside);

        var repeated = await service.EvaluateAndTriggerAsync(insidePosition, context, [poi]);
        repeated.Reason.Should().Be(TriggerDecisionReason.AlreadyTriggeredForVisit);
        fakePlayer.DispatchedCount.Should().Be(1);
        Print("GPS lặp", insideDistance, repeated);

        // Lượt ghé khác để nhánh Safety không bị che bởi kết quả chống lặp ở trên.
        var safetyContext = context with { VisitId = "safety-demo", IsSafetyAnnouncementActive = true };
        var safety = await service.EvaluateAndTriggerAsync(insidePosition, safetyContext, [poi]);
        safety.Reason.Should().Be(TriggerDecisionReason.SafetyAnnouncementActive);
        fakePlayer.DispatchedCount.Should().Be(1);
        Print("Safety đang mở", insideDistance, safety);

        output.WriteLine($"Tổng yêu cầu gửi tới player giả: {fakePlayer.DispatchedCount}; loa thật: chưa kiểm chứng.");

        void Print(string situation, double distanceMeters, PoiAudioTriggerEvaluationResult result) =>
            output.WriteLine($"{situation} | {distanceMeters:F1} m | " +
                $"{(result.ShouldTrigger ? "GỬI LỆNH" : "KHÔNG GỬI")} | {result.Reason}");
    }
}
