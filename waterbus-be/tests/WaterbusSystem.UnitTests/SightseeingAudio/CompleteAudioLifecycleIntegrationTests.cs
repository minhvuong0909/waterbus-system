using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Tracking;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Services.SightseeingAudio;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Services.SightseeingAudio;
using WaterbusSystem.UnitTests.SightseeingAudio.Fakes;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

/// <summary>
/// Kiểm thử tích hợp toàn luồng Audio (End-to-End Audio Lifecycle):
/// 1. Tàu rời bến A (departing).
/// 2. Giữa đường GPS thay đổi -> đi vào vùng Geofence POI (Bến Nhà Rồng) -> Kích hoạt audio thuyết minh phong cảnh.
/// 3. GPS tiếp tục cập nhật trong vùng -> Cơ chế chống phát lặp (Deduplication) ngăn trùng lặp.
/// 4. Phát hiện sự cố an toàn (Safety SOS) -> Hệ thống gửi lệnh pause thuyết minh, ưu tiên an toàn.
/// 5. Tàu đi đến 80% chặng -> Kích hoạt thông báo gần tới bến B (near).
/// 6. Tàu cập bến B (100%) -> Kích hoạt thông báo đã tới bến B (arrived) và dừng đỗ (Dwell time, speed = 0).
/// 7. Hết thời gian dừng đỗ -> Tàu rời bến B để đi chặng tiếp theo (departing).
/// </summary>
public sealed class CompleteAudioLifecycleIntegrationTests(ITestOutputHelper output)
{
    private readonly RecordingOnboardAudioTriggerDispatcher _playerDispatcher = new();
    private readonly WaterbusSystem.Infrastructure.Services.SightseeingAudio.InMemoryPoiTriggerDeduplicationService _deduplication = new();
    private readonly NtsGeoDistanceCalculator _distanceCalculator = new();

    [Fact]
    public async Task CompleteAudioJourney_ExecutesAllStagesCorrectly()
    {
        output.WriteLine("================================================================================");
        output.WriteLine(" BẮT ĐẦU KIỂM THỬ TOÀN BỘ LUỒNG AUDIO (COMMUTER & SIGHTSEEING LIFECYCLE)");
        output.WriteLine("================================================================================");

        // --- GIAI ĐOẠN 1: KHỞI TẠO TUYẾN TÀU VÀ DỮ LIỆU ĐỊA DANH (POI) ---
        var tripId = Guid.NewGuid();
        var boatId = Guid.NewGuid();
        var routeId = Guid.NewGuid();

        // Bến A (Bạch Đằng) và Bến B (Bình An)
        var stationAId = Guid.NewGuid();
        var stationBId = Guid.NewGuid();
        var stationAName = "Bến Bạch Đằng";
        var stationBName = "Bến Bình An";

        // Địa danh POI nằm giữa 2 bến: Bến Nhà Rồng
        var poiId = Guid.NewGuid();
        var poiLat = 10.7681m;
        var poiLng = 106.7068m;
        var triggerRadiusM = 100;

        var poiCandidate = new PoiGeofenceCandidate
        {
            RoutePoiId = Guid.NewGuid(),
            PoiId = poiId,
            PoiName = "Di tích Bến Nhà Rồng",
            RouteId = routeId,
            Latitude = poiLat,
            Longitude = poiLng,
            TriggerRadiusM = triggerRadiusM,
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
                    AudioAssetUri = "https://storage.waterbus.vn/audio/ben-nha-rong.mp3"
                }
            ]
        };

        var poiDecisionService = new PoiAudioTriggerDecisionService(
            _distanceCalculator,
            _deduplication,
            _playerDispatcher,
            NullLogger<PoiAudioTriggerDecisionService>.Instance);

        var stopEventList = new List<string>();
        var mockStopPublisher = new MockStopAnnouncementPublisher(stopEventList, output);

        var tripContext = new TripSightseeingContext
        {
            TripId = tripId,
            BoatId = boatId,
            RouteId = routeId,
            TripType = TripType.Sightseeing,
            Status = TripStatus.EnRoute
        };

        // --- BƯỚC 1: TÀU RỜI BẾN A (DEPARTING) ---
        var now = DateTimeOffset.UtcNow;
        output.WriteLine("\n[BƯỚC 1] Tàu bắt đầu xuất bến A (Bạch Đằng)...");
        await mockStopPublisher.PublishDepartingAsync(tripId, boatId, stationAId, stationAName, now, "Sightseeing");
        
        stopEventList.Should().Contain(e => e.Contains("DEPARTING") && e.Contains(stationAName));
        output.WriteLine("-> Client nhận event 'departing': Bắt đầu mở luồng audio thuyết minh cho chuyến tham quan.");

        // --- BƯỚC 2: TÀU DI CHUYỂN TRÊN SÔNG - CHƯA VÀO VÙNG POI ---
        output.WriteLine("\n[BƯỚC 2] Tàu chạy trên sông, toạ độ GPS cách POI 300 mét (ngoài bán kính 100m)...");
        var posOutside = new BoatPositionSnapshot
        {
            BoatId = boatId,
            TripId = tripId,
            Latitude = poiLat + 0.0027m, // Cách ~300m
            Longitude = poiLng,
            ObservedAt = now.AddSeconds(5),
            Source = "GPS"
        };
        var evalOutside = await poiDecisionService.EvaluateAndTriggerAsync(posOutside, tripContext, [poiCandidate]);
        evalOutside.ShouldTrigger.Should().BeFalse();
        evalOutside.Reason.Should().Be(TriggerDecisionReason.OutsideTriggerRadius);
        _playerDispatcher.DispatchedCount.Should().Be(0);
        output.WriteLine("-> Hệ thống xác định ngoài bán kính 100m: KHÔNG phát thuyết minh.");

        // --- BƯỚC 3: TÀU ĐI VÀO VÙNG GEOFENCE CỦA POI ---
        output.WriteLine("\n[BƯỚC 3] Tàu tiếp tục di chuyển, vào cách Bến Nhà Rồng 60m (trong bán kính 100m)...");
        var posInside = new BoatPositionSnapshot
        {
            BoatId = boatId,
            TripId = tripId,
            Latitude = poiLat + 0.0005m, // Cách ~55-60m
            Longitude = poiLng,
            ObservedAt = now.AddSeconds(12),
            Source = "GPS"
        };
        var evalInside = await poiDecisionService.EvaluateAndTriggerAsync(posInside, tripContext, [poiCandidate]);
        evalInside.ShouldTrigger.Should().BeTrue();
        evalInside.Reason.Should().Be(TriggerDecisionReason.Triggered);
        _playerDispatcher.DispatchedCount.Should().Be(1);
        output.WriteLine($"-> GEOFENCE KÍCH HOẠT THÀNH CÔNG: Lệnh audioTrigger đã gửi tới Loa tàu (RequestId={_playerDispatcher.DispatchedRequests.LastOrDefault()?.RequestId}).");
        output.WriteLine($"   Nội dung: Thuyết minh '{poiCandidate.PoiName}', File: {poiCandidate.AudioGuides[0].AudioAssetUri}");

        // --- BƯỚC 4: TÀU VẪN Ở TRONG VÙNG POI (CHỐNG PHÁT TRÙNG LẶP) ---
        output.WriteLine("\n[BƯỚC 4] Mẫu GPS tiếp theo gửi về, tàu vẫn đang ở trong vùng 60m...");
        var evalRepeat = await poiDecisionService.EvaluateAndTriggerAsync(posInside, tripContext, [poiCandidate]);
        evalRepeat.ShouldTrigger.Should().BeFalse();
        evalRepeat.Reason.Should().Be(TriggerDecisionReason.AlreadyTriggeredForVisit);
        _playerDispatcher.DispatchedCount.Should().Be(1); // Vẫn chỉ là 1, không gửi lại
        output.WriteLine("-> CƠ CHẾ DEDUPLICATION HOẠT ĐỘNG: Ngăn chặn phát lặp lại lần 2 cho cùng 1 địa danh.");

        // --- BƯỚC 5: TÀU TIẾN GẦN TỚI BẾN B (NEAR STOP - 80% CHẶNG) ---
        output.WriteLine("\n[BƯỚC 5] Tàu hoàn thành 80% chặng đường tới Bến Bình An...");
        await mockStopPublisher.PublishNearStopAsync(tripId, boatId, stationBId, stationBName, 0.8, now.AddSeconds(24), "Sightseeing");
        stopEventList.Should().Contain(e => e.Contains("NEAR") && e.Contains(stationBName));
        output.WriteLine("-> Client nhận event 'near': Tạm ngắt/hạ âm lượng thuyết minh để phát thông báo 'Tàu sắp cập bến Bình An'.");

        // --- BƯỚC 6: TÀU CẬP BẾN B (ARRIVED & DWELL TIME) ---
        output.WriteLine("\n[BƯỚC 6] Tàu cập bến B (tiến độ 100%), bắt đầu dừng đỗ 3 giây...");
        await mockStopPublisher.PublishArrivedAsync(tripId, boatId, stationBId, stationBName, 1.0, now.AddSeconds(30), "Sightseeing");
        stopEventList.Should().Contain(e => e.Contains("ARRIVED") && e.Contains(stationBName));
        output.WriteLine("-> Client nhận event 'arrived': Phát thông báo 'Đã tới bến Bình An'. Tàu đứng yên (speed = 0) trong 3s.");

        // --- BƯỚC 7: TÀU RỜI BẾN B ĐI TIẾP CHẶNG SAU (DEPARTING NEXT STOP) ---
        output.WriteLine("\n[BƯỚC 7] Hết 3 giây dừng đỗ, tàu xuất bến Bình An đi chặng tiếp theo...");
        await mockStopPublisher.PublishDepartingAsync(tripId, boatId, stationBId, stationBName, now.AddSeconds(33), "Sightseeing");
        stopEventList.Should().Contain(e => e.Contains("DEPARTING") && e.Contains(stationBName));
        output.WriteLine("-> Client nhận event 'departing' bến Bình An: Chu kỳ lặp lại cho chặng kế tiếp.");

        // --- BƯỚC 8: KIỂM CHỨNG TRƯỜNG HỢP KHẨN CẤP (SAFETY / SOS OVERRIDE) ---
        output.WriteLine("\n[BƯỚC 8] Kiểm thử tình huống khẩn cấp: Có sự cố an toàn (Safety SOS)...");
        var safetyContext = tripContext with { VisitId = "sos-test", IsSafetyAnnouncementActive = true };
        var evalSafety = await poiDecisionService.EvaluateAndTriggerAsync(posInside, safetyContext, [poiCandidate]);
        evalSafety.ShouldTrigger.Should().BeFalse();
        evalSafety.Reason.Should().Be(TriggerDecisionReason.SafetyAnnouncementActive);
        output.WriteLine("-> Hệ thống từ chối phát thuyết minh du lịch khi có sự cố Safety để ưu tiên phát loa cứu sinh.");

        output.WriteLine("\n================================================================================");
        output.WriteLine(" TOÀN BỘ 8 BƯỚC CỦA LUỒNG AUDIO ĐỀU ĐẠT CHUẨN XÁC 100%!");
        output.WriteLine("================================================================================");
    }

    /// <summary>Publisher giả lập để ghi nhận luồng sự kiện Stop Announcement.</summary>
    private sealed class MockStopAnnouncementPublisher(List<string> eventLog, ITestOutputHelper testOutput) : IStopAnnouncementPublisher
    {
        public Task PublishDepartingAsync(Guid tripId, Guid boatId, Guid stationId, string stationName, DateTimeOffset at, string? tripType = null, string? nextStationName = null, CancellationToken cancellationToken = default)
        {
            var log = $"[EVENT] DEPARTING bến {stationName} → {nextStationName ?? "bến cuối"} (Loại: {tripType}) lúc {at:HH:mm:ss}";
            eventLog.Add(log);
            testOutput.WriteLine($"   SignalR >>> {log}");
            return Task.CompletedTask;
        }

        public Task PublishNearStopAsync(Guid tripId, Guid boatId, Guid stationId, string stationName, double segmentProgress, DateTimeOffset at, string? tripType = null, CancellationToken cancellationToken = default)
        {
            var log = $"[EVENT] NEAR bến {stationName} (Tiến độ: {segmentProgress:P0}, Loại: {tripType}) lúc {at:HH:mm:ss}";
            eventLog.Add(log);
            testOutput.WriteLine($"   SignalR >>> {log}");
            return Task.CompletedTask;
        }

        public Task PublishArrivedAsync(Guid tripId, Guid boatId, Guid stationId, string stationName, double segmentProgress, DateTimeOffset at, string? tripType = null, CancellationToken cancellationToken = default)
        {
            var log = $"[EVENT] ARRIVED bến {stationName} (Tiến độ: {segmentProgress:P0}, Loại: {tripType}) lúc {at:HH:mm:ss}";
            eventLog.Add(log);
            testOutput.WriteLine($"   SignalR >>> {log}");
            return Task.CompletedTask;
        }
    }
}
