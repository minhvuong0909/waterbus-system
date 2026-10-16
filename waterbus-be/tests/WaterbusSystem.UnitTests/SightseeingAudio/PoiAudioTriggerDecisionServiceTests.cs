using FluentAssertions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Application.Features.SightseeingAudio.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.UnitTests.SightseeingAudio.Fakes;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

public class PoiAudioTriggerDecisionServiceTests
{
    private readonly IGeoDistanceCalculator _distanceCalculator = new NtsGeoDistanceCalculator();
    private readonly InMemoryPoiTriggerDeduplicationService _dedupService = new();
    private readonly RecordingOnboardAudioTriggerDispatcher _dispatcher = new();
    private readonly PoiAudioTriggerDecisionService _service;

    // Tọa độ mẫu quanh TP.HCM:
    // Bến Bạch Đằng: 10.774968, 106.704710
    // Bến Nhà Rồng:  10.768462, 106.705513 (cách Bạch Đằng ≈ 725m)
    private const decimal BachDangLat = 10.774968m;
    private const decimal BachDangLng = 106.704710m;
    private const decimal NhaRongLat = 10.768462m;
    private const decimal NhaRongLng = 106.705513m;

    private readonly Guid _boatId = Guid.NewGuid();
    private readonly Guid _tripId = Guid.NewGuid();
    private readonly Guid _routeId = Guid.NewGuid();
    private readonly Guid _poiId = Guid.NewGuid();
    private readonly Guid _audioGuideId = Guid.NewGuid();

    public PoiAudioTriggerDecisionServiceTests()
    {
        _service = new PoiAudioTriggerDecisionService(
            _distanceCalculator,
            _dedupService,
            _dispatcher);
    }

    private TripSightseeingContext CreateValidTripContext(Action<TripSightseeingContext>? customize = null)
    {
        var context = new TripSightseeingContext
        {
            TripId = _tripId,
            BoatId = _boatId,
            RouteId = _routeId,
            TripType = TripType.Sightseeing,
            Status = TripStatus.EnRoute,
            IsSafetyAnnouncementActive = false,
            ActiveSafetyNotice = null,
            VisitId = null
        };

        if (customize != null)
        {
            // Cho phép override nếu cần
        }

        return context;
    }

    private PoiGeofenceCandidate CreateValidPoiCandidate(
        decimal lat = BachDangLat,
        decimal lng = BachDangLng,
        int? triggerRadiusM = 100,
        int displayOrder = 1,
        bool isPoiActive = true,
        bool isRoutePoiActive = true,
        string languageCode = "vi",
        bool isAudioPublished = true,
        string audioUri = "https://storage.example.com/audio/bachdang-vi.mp3")
    {
        return new PoiGeofenceCandidate
        {
            PoiId = _poiId,
            PoiName = "Bến Bạch Đằng",
            RouteId = _routeId,
            Latitude = lat,
            Longitude = lng,
            TriggerRadiusM = triggerRadiusM,
            DisplayOrder = displayOrder,
            IsPoiActive = isPoiActive,
            IsRoutePoiActive = isRoutePoiActive,
            AudioGuides = new List<AudioGuideCandidate>
            {
                new()
                {
                    AudioGuideId = _audioGuideId,
                    PoiId = _poiId,
                    LanguageCode = languageCode,
                    AudioAssetUri = audioUri,
                    DurationSeconds = 120,
                    IsPublished = isAudioPublished
                }
            }
        };
    }

    private BoatPositionSnapshot CreateBoatPosition(decimal lat, decimal lng, Guid? boatId = null, Guid? tripId = null)
    {
        return new BoatPositionSnapshot
        {
            BoatId = boatId ?? _boatId,
            TripId = tripId ?? _tripId,
            Latitude = lat,
            Longitude = lng,
            ObservedAt = DateTimeOffset.UtcNow,
            Source = "GPS",
            SpeedKnots = 8.5m
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. VÀO VÙNG BÁN KÍNH (Happy Path)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_BoatInsideTriggerRadius_DispatchesTriggerRequestAndMarksDeduplication()
    {
        // Tàu nằm ngay tại tọa độ POI (khoảng cách = 0m, bán kính 100m)
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.Should().NotBeNull();
        result.ShouldTrigger.Should().BeTrue();
        result.Reason.Should().Be(TriggerDecisionReason.Triggered);
        result.TriggeredPoiId.Should().Be(_poiId);
        result.TriggeredAudioGuideId.Should().Be(_audioGuideId);
        result.DistanceMeters.Should().Be(0.0);
        result.TriggerRadiusMeters.Should().Be(100);

        // Chưa có ACK từ Onboard Player -> HasOnboardPlaybackAcknowledged LUÔN là false (BR-EXP-03)
        result.HasOnboardPlaybackAcknowledged.Should().BeFalse(
            "Backend chỉ gửi yêu cầu; chưa có ACK từ Onboard Player thì không được báo đã phát ra loa.");

        // Dispatcher phải nhận đúng 1 request
        _dispatcher.DispatchedCount.Should().Be(1);
        var dispatched = _dispatcher.DispatchedRequests[0];
        dispatched.TripId.Should().Be(_tripId);
        dispatched.BoatId.Should().Be(_boatId);
        dispatched.RouteId.Should().Be(_routeId);
        dispatched.PoiId.Should().Be(_poiId);
        dispatched.AudioAssetUri.Should().Be("https://storage.example.com/audio/bachdang-vi.mp3");

        // Deduplication service phải được đánh dấu đã kích hoạt
        var hasTriggered = await _dedupService.HasBeenTriggeredAsync(_tripId, _poiId);
        hasTriggered.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. RA NGOÀI VÙNG BÁN KÍNH
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_BoatOutsideTriggerRadius_DoesNotTrigger()
    {
        // Tàu ở Bến Nhà Rồng, POI ở Bến Bạch Đằng (~725m), bán kính chỉ 100m
        var boatPos = CreateBoatPosition(NhaRongLat, NhaRongLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.OutsideTriggerRadius);
        _dispatcher.DispatchedCount.Should().Be(0);

        var hasTriggered = await _dedupService.HasBeenTriggeredAsync(_tripId, _poiId);
        hasTriggered.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. ĐÚNG BIÊN BÁN KÍNH (Edge Boundary)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_ExactBoundary_TriggersWhenWithinOrEqual()
    {
        // Tính khoảng cách chính xác giữa Nhà Rồng và Bạch Đằng
        var exactDistance = _distanceCalculator.CalculateDistanceMeters(
            NhaRongLat, NhaRongLng, BachDangLat, BachDangLng);

        // Đặt bán kính trần làm tròn lên để chứa đúng khoảng cách
        int boundaryRadius = (int)Math.Ceiling(exactDistance);

        var boatPos = CreateBoatPosition(NhaRongLat, NhaRongLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(triggerRadiusM: boundaryRadius);

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeTrue();
        result.Reason.Should().Be(TriggerDecisionReason.Triggered);
    }

    [Fact]
    public async Task EvaluateAndTrigger_JustOutsideBoundary_DoesNotTrigger()
    {
        var exactDistance = _distanceCalculator.CalculateDistanceMeters(
            NhaRongLat, NhaRongLng, BachDangLat, BachDangLng);

        // Bán kính nhỏ hơn khoảng cách thực tế 1 mét
        int justInsideRadius = (int)Math.Floor(exactDistance) - 1;

        var boatPos = CreateBoatPosition(NhaRongLat, NhaRongLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(triggerRadiusM: justInsideRadius);

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.OutsideTriggerRadius);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. GPS GỬI NHIỀU MẪU LẶP (Deduplication BR-EXP-05)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_MultipleGpsSamplesInZone_TriggersOnlyOnce()
    {
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        // Mẫu 1: Tàu vào vùng
        var pos1 = CreateBoatPosition(BachDangLat, BachDangLng);
        var result1 = await _service.EvaluateAndTriggerAsync(pos1, tripContext, new[] { candidate });
        result1.ShouldTrigger.Should().BeTrue();
        result1.Reason.Should().Be(TriggerDecisionReason.Triggered);
        _dispatcher.DispatchedCount.Should().Be(1);

        // Mẫu 2..5: GPS tiếp tục gửi vị trí trong vùng
        for (int i = 0; i < 4; i++)
        {
            var posNext = CreateBoatPosition(BachDangLat, BachDangLng);
            var resultNext = await _service.EvaluateAndTriggerAsync(posNext, tripContext, new[] { candidate });

            resultNext.ShouldTrigger.Should().BeFalse();
            resultNext.Reason.Should().Be(TriggerDecisionReason.AlreadyTriggeredForVisit);
        }

        // Vẫn chỉ có duy nhất 1 request được gửi tới dispatcher
        _dispatcher.DispatchedCount.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 5. HAI POI CÙNG TRONG BÁN KÍNH
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_TwoOverlappingPoisWithoutProgress_DoesNotGuess()
    {
        var tripContext = CreateValidTripContext();

        // POI 1: Trùng điểm với tàu (cách 0m)
        var poi1 = CreateValidPoiCandidate(lat: BachDangLat, lng: BachDangLng, triggerRadiusM: 1000, displayOrder: 2);

        // POI 2: Cách tàu ~725m (vẫn trong bán kính 1000m)
        var poi2Id = Guid.NewGuid();
        var poi2 = new PoiGeofenceCandidate
        {
            PoiId = poi2Id,
            PoiName = "Bến Nhà Rồng",
            RouteId = _routeId,
            Latitude = NhaRongLat,
            Longitude = NhaRongLng,
            TriggerRadiusM = 1000,
            DisplayOrder = 1,
            IsPoiActive = true,
            IsRoutePoiActive = true,
            AudioGuides = new List<AudioGuideCandidate>
            {
                new()
                {
                    AudioGuideId = Guid.NewGuid(),
                    PoiId = poi2Id,
                    LanguageCode = "vi",
                    AudioAssetUri = "https://storage.example.com/audio/nharong-vi.mp3",
                    DurationSeconds = 90,
                    IsPublished = true
                }
            }
        };

        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { poi2, poi1 });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.AmbiguousRouteProgress);
        _dispatcher.DispatchedCount.Should().Be(0);

        var nextPoiContext = tripContext with { NextRoutePoiDisplayOrder = 1 };
        var orderedResult = await _service.EvaluateAndTriggerAsync(boatPos, nextPoiContext, new[] { poi2, poi1 });
        orderedResult.ShouldTrigger.Should().BeTrue();
        orderedResult.TriggeredPoiId.Should().Be(poi2Id, "RoutePoi thứ tự 1 được ưu tiên dù ở xa hơn");
        _dispatcher.DispatchedCount.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAndTrigger_SamePoiAtTwoRouteOccurrences_DeduplicatesEachOccurrence()
    {
        var firstRoutePoiId = Guid.NewGuid();
        var secondRoutePoiId = Guid.NewGuid();
        var first = CreateValidPoiCandidate(displayOrder: 1) with { RoutePoiId = firstRoutePoiId };
        var second = CreateValidPoiCandidate(displayOrder: 2) with { RoutePoiId = secondRoutePoiId };
        var position = CreateBoatPosition(BachDangLat, BachDangLng);
        var context = CreateValidTripContext();

        var firstResult = await _service.EvaluateAndTriggerAsync(position,
            context with { NextRoutePoiDisplayOrder = 1 }, new[] { first, second });
        var firstRepeat = await _service.EvaluateAndTriggerAsync(position,
            context with { NextRoutePoiDisplayOrder = 1 }, new[] { first, second });
        var secondResult = await _service.EvaluateAndTriggerAsync(position,
            context with { NextRoutePoiDisplayOrder = 2 }, new[] { first, second });

        firstResult.ShouldTrigger.Should().BeTrue();
        firstRepeat.Reason.Should().Be(TriggerDecisionReason.AlreadyTriggeredForVisit);
        secondResult.ShouldTrigger.Should().BeTrue();
        _dispatcher.DispatchedCount.Should().Be(2);
        firstResult.TriggerRequest!.RoutePoiId.Should().Be(firstRoutePoiId);
        secondResult.TriggerRequest!.RoutePoiId.Should().Be(secondRoutePoiId);
    }

    [Fact]
    public async Task EvaluateAndTrigger_RepeatedPoiWithoutRoutePoiId_DoesNotConflateOccurrences()
    {
        var first = CreateValidPoiCandidate(displayOrder: 1);
        var second = CreateValidPoiCandidate(displayOrder: 2);

        var result = await _service.EvaluateAndTriggerAsync(
            CreateBoatPosition(BachDangLat, BachDangLng),
            CreateValidTripContext() with { NextRoutePoiDisplayOrder = 1 },
            new[] { first, second });

        result.Reason.Should().Be(TriggerDecisionReason.InvalidTripContext);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_SentWithoutPlayerAck_RetriesWithSameRequestId()
    {
        var position = CreateBoatPosition(BachDangLat, BachDangLng);
        var context = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();
        _dispatcher.ShouldOmitPlayerAck = true;

        var unacknowledged = await _service.EvaluateAndTriggerAsync(position, context, new[] { candidate });
        _dispatcher.ShouldOmitPlayerAck = false;
        var acknowledged = await _service.EvaluateAndTriggerAsync(position, context, new[] { candidate });

        unacknowledged.Reason.Should().Be(TriggerDecisionReason.DispatchFailed);
        acknowledged.ShouldTrigger.Should().BeTrue();
        _dispatcher.DispatchedRequests.Should().HaveCount(2);
        _dispatcher.DispatchedRequests[0].RequestId.Should().Be(_dispatcher.DispatchedRequests[1].RequestId);
        acknowledged.HasOnboardPlaybackAcknowledged.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 6. SAI BOAT / TRIP / ROUTE
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_MismatchedBoatId_DoesNotTrigger()
    {
        var differentBoatId = Guid.NewGuid();
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng, boatId: differentBoatId);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.BoatTripMismatch);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_MismatchedTripId_DoesNotTrigger()
    {
        var differentTripId = Guid.NewGuid();
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng, tripId: differentTripId);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.BoatTripMismatch);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_PoiBelongsToDifferentRoute_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();

        var differentRouteId = Guid.NewGuid();
        var candidate = new PoiGeofenceCandidate
        {
            PoiId = _poiId,
            PoiName = "Bến Bạch Đằng",
            RouteId = differentRouteId, // Tuyến khác
            Latitude = BachDangLat,
            Longitude = BachDangLng,
            TriggerRadiusM = 100,
            DisplayOrder = 1,
            IsPoiActive = true,
            IsRoutePoiActive = true,
            AudioGuides = new List<AudioGuideCandidate>
            {
                new()
                {
                    AudioGuideId = _audioGuideId,
                    PoiId = _poiId,
                    LanguageCode = "vi",
                    AudioAssetUri = "https://example.com/audio.mp3",
                    IsPublished = true
                }
            }
        };

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.NoCandidatePois);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 7. CHUYẾN COMMUTER (BR-EXP-01)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_CommuterTrip_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = new TripSightseeingContext
        {
            TripId = _tripId,
            BoatId = _boatId,
            RouteId = _routeId,
            TripType = TripType.Commuter, // Chuyến thường
            Status = TripStatus.EnRoute
        };
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.TripNotSightseeing);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 8. AUDIOGUIDE CHƯA PUBLISH HOẶC KHÔNG PHẢI TIẾNG VIỆT
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_AudioGuideNotPublished_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(isAudioPublished: false);

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.NoPublishedVietnameseAudio);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_OnlyEnglishAudioAvailable_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(languageCode: "en"); // Không có tiếng Việt

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.NoPublishedVietnameseAudio);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_EmptyAudioUri_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(audioUri: "  "); // URI rỗng

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.NoPublishedVietnameseAudio);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 9. BÁN KÍNH NULL HOẶC KHÔNG HỢP LỆ
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_TriggerRadiusNull_DoesNotTriggerWithoutDefault()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(triggerRadiusM: null); // Null

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.TriggerRadiusMissingOrInvalid);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task EvaluateAndTrigger_TriggerRadiusZeroOrNegative_DoesNotTrigger(int invalidRadius)
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(triggerRadiusM: invalidRadius);

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.TriggerRadiusMissingOrInvalid);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 10. TRẠNG THÁI DỪNG VÀ KHÔNG ĐANG CHẠY (BR-EXP-06, BR-INC-05)
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TripStatus.Suspended)]
    [InlineData(TripStatus.Terminated)]
    [InlineData(TripStatus.Scheduled)]
    [InlineData(TripStatus.Boarding)]
    [InlineData(TripStatus.Arrived)]
    [InlineData(TripStatus.Completed)]
    [InlineData(TripStatus.Cancelled)]
    public async Task EvaluateAndTrigger_TripNotEnRoute_DoesNotTrigger(TripStatus status)
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = new TripSightseeingContext
        {
            TripId = _tripId,
            BoatId = _boatId,
            RouteId = _routeId,
            TripType = TripType.Sightseeing,
            Status = status
        };
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.TripNotEnRoute);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 11. THÔNG BÁO AN TOÀN / CAPTAIN OVERRIDE (BR-EXP-06)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_SafetyAnnouncementActive_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = new TripSightseeingContext
        {
            TripId = _tripId,
            BoatId = _boatId,
            RouteId = _routeId,
            TripType = TripType.Sightseeing,
            Status = TripStatus.EnRoute,
            IsSafetyAnnouncementActive = true,
            ActiveSafetyNotice = "Thời tiết xấu, yêu cầu hành khách mặc áo phao"
        };
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.SafetyAnnouncementActive);
        result.Message.Should().Contain("nhường ưu tiên");
        result.Message.Should().NotContain("Thời tiết xấu", "nội dung cảnh báo nội bộ không cần đưa ra response");
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 12. POI HOẶC ROUTEPOI INACTIVE
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_PoiInactive_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(isPoiActive: false);

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.PoiInactive);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_RoutePoiInactive_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate(isRoutePoiActive: false);

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.RoutePoiInactive);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 13. XỬ LÝ LỖI DISPATCH (Không ghi nhận đã phát khi gửi lệnh thất bại)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_DispatchFails_DoesNotMarkDeduplication()
    {
        _dispatcher.ShouldFailDispatch = true;
        _dispatcher.FailureMessage = "Mất kết nối mạng IoT trên tàu.";

        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.DispatchFailed);
        result.Message.Should().NotContain("Mất kết nối mạng IoT", "lỗi transport không được lộ ra response");

        // Khi dispatch thất bại, KHÔNG được đánh dấu deduplication để cho phép thử lại
        var hasTriggered = await _dedupService.HasBeenTriggeredAsync(_tripId, _poiId);
        hasTriggered.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 14. RESET CHUYẾN VÀ LƯỢT GHÉ MỚI (VisitId Boundary)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_NewVisitId_AllowsTriggerForSamePoi()
    {
        var candidate = CreateValidPoiCandidate();
        var pos = CreateBoatPosition(BachDangLat, BachDangLng);

        // Lượt ghé 1
        var contextVisit1 = new TripSightseeingContext
        {
            TripId = _tripId,
            BoatId = _boatId,
            RouteId = _routeId,
            TripType = TripType.Sightseeing,
            Status = TripStatus.EnRoute,
            VisitId = "lap-1"
        };

        var result1 = await _service.EvaluateAndTriggerAsync(pos, contextVisit1, new[] { candidate });
        result1.ShouldTrigger.Should().BeTrue();
        _dispatcher.DispatchedCount.Should().Be(1);

        // Mẫu tiếp theo của lượt ghé 1 bị chặn
        var result1Repeat = await _service.EvaluateAndTriggerAsync(pos, contextVisit1, new[] { candidate });
        result1Repeat.ShouldTrigger.Should().BeFalse();
        result1Repeat.Reason.Should().Be(TriggerDecisionReason.AlreadyTriggeredForVisit);

        // Lượt ghé 2 (tuyến vòng quay lại)
        var contextVisit2 = new TripSightseeingContext
        {
            TripId = _tripId,
            BoatId = _boatId,
            RouteId = _routeId,
            TripType = TripType.Sightseeing,
            Status = TripStatus.EnRoute,
            VisitId = "lap-2"
        };

        var result2 = await _service.EvaluateAndTriggerAsync(pos, contextVisit2, new[] { candidate });
        result2.ShouldTrigger.Should().BeTrue("Lượt ghé mới (lap-2) cho phép kích hoạt lại POI");
        _dispatcher.DispatchedCount.Should().Be(2);
    }

    [Fact]
    public async Task EvaluateAndTrigger_TripReset_AllowsTriggerAgain()
    {
        var candidate = CreateValidPoiCandidate();
        var pos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();

        var result1 = await _service.EvaluateAndTriggerAsync(pos, tripContext, new[] { candidate });
        result1.ShouldTrigger.Should().BeTrue();

        // Reset trạng thái chuyến
        await _dedupService.ResetForTripAsync(_tripId);

        // Thử lại sau reset
        var result2 = await _service.EvaluateAndTriggerAsync(pos, tripContext, new[] { candidate });
        result2.ShouldTrigger.Should().BeTrue("Sau khi reset chuyến, POI có thể được kích hoạt lại");
        _dispatcher.DispatchedCount.Should().Be(2);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 15. TỌA ĐỘ BẤT THƯỜNG / VALIDATION CƠ BẢN
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndTrigger_InvalidBoatCoordinates_ReturnsSkippedWithoutException()
    {
        var invalidPos = CreateBoatPosition(95m, 106.7m); // Vĩ độ > 90
        var tripContext = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        var result = await _service.EvaluateAndTriggerAsync(invalidPos, tripContext, new[] { candidate });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.InvalidBoatPosition);
    }

    [Fact]
    public async Task EvaluateAndTrigger_PositionWithoutTripId_DoesNotTrigger()
    {
        var position = CreateBoatPosition(BachDangLat, BachDangLng) with { TripId = null };

        var result = await _service.EvaluateAndTriggerAsync(position, CreateValidTripContext(),
            new[] { CreateValidPoiCandidate() });

        result.Reason.Should().Be(TriggerDecisionReason.InvalidBoatPosition);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_PositionWithoutTimestamp_DoesNotTrigger()
    {
        var position = CreateBoatPosition(BachDangLat, BachDangLng) with { ObservedAt = default };

        var result = await _service.EvaluateAndTriggerAsync(position, CreateValidTripContext(),
            new[] { CreateValidPoiCandidate() });

        result.Reason.Should().Be(TriggerDecisionReason.InvalidBoatPosition);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_AudioBelongsToDifferentPoi_DoesNotTrigger()
    {
        var candidate = CreateValidPoiCandidate();
        var wrongAudio = candidate.AudioGuides.Single() with { PoiId = Guid.NewGuid() };

        var result = await _service.EvaluateAndTriggerAsync(
            CreateBoatPosition(BachDangLat, BachDangLng), CreateValidTripContext(),
            new[] { candidate with { AudioGuides = new[] { wrongAudio } } });

        result.Reason.Should().Be(TriggerDecisionReason.NoPublishedVietnameseAudio);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Theory]
    [InlineData("https://storage.example.test/audio.mp3?sig=short-lived")]
    [InlineData("C:\\temp\\audio.mp3")]
    [InlineData("/tmp/audio.mp3")]
    [InlineData("file:///tmp/audio.mp3")]
    public async Task EvaluateAndTrigger_TemporaryOrSignedAudioUri_DoesNotTrigger(string audioUri)
    {
        var result = await _service.EvaluateAndTriggerAsync(
            CreateBoatPosition(BachDangLat, BachDangLng), CreateValidTripContext(),
            new[] { CreateValidPoiCandidate(audioUri: audioUri) });

        result.Reason.Should().Be(TriggerDecisionReason.NoPublishedVietnameseAudio);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_InvalidPoiCoordinates_DoesNotCallDistanceCalculator()
    {
        var result = await _service.EvaluateAndTriggerAsync(
            CreateBoatPosition(BachDangLat, BachDangLng), CreateValidTripContext(),
            new[] { CreateValidPoiCandidate(lat: 95m) });

        result.Reason.Should().Be(TriggerDecisionReason.PoiCoordinatesInvalid);
        _dispatcher.DispatchedCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAndTrigger_ConcurrentGpsSamples_OnlyOneDispatches()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.DispatchStarted = started;
        _dispatcher.DispatchGate = release.Task;
        var position = CreateBoatPosition(BachDangLat, BachDangLng);
        var context = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        var firstTask = _service.EvaluateAndTriggerAsync(position, context, new[] { candidate });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await _service.EvaluateAndTriggerAsync(position, context, new[] { candidate });
        release.SetResult(true);
        var first = await firstTask;

        first.ShouldTrigger.Should().BeTrue();
        second.Reason.Should().Be(TriggerDecisionReason.AlreadyTriggeredForVisit);
        _dispatcher.DispatchedCount.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAndTrigger_DispatchThrows_ReleasesReservationForRetry()
    {
        _dispatcher.DispatchException = new InvalidOperationException("internal transport detail");
        var position = CreateBoatPosition(BachDangLat, BachDangLng);
        var context = CreateValidTripContext();
        var candidate = CreateValidPoiCandidate();

        var failed = await _service.EvaluateAndTriggerAsync(position, context, new[] { candidate });
        _dispatcher.DispatchException = null;
        var retry = await _service.EvaluateAndTriggerAsync(position, context, new[] { candidate });

        failed.Reason.Should().Be(TriggerDecisionReason.DispatchFailed);
        failed.Message.Should().NotContain("internal transport detail");
        retry.ShouldTrigger.Should().BeTrue();
        _dispatcher.DispatchedCount.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAndTrigger_PoiMissingCoordinates_DoesNotTrigger()
    {
        var boatPos = CreateBoatPosition(BachDangLat, BachDangLng);
        var tripContext = CreateValidTripContext();

        var candidateWithoutCoords = new PoiGeofenceCandidate
        {
            PoiId = _poiId,
            PoiName = "POI chưa có tọa độ",
            RouteId = _routeId,
            Latitude = null,
            Longitude = null,
            TriggerRadiusM = 100,
            AudioGuides = new List<AudioGuideCandidate>
            {
                new() { AudioGuideId = _audioGuideId, LanguageCode = "vi", IsPublished = true, AudioAssetUri = "uri" }
            }
        };

        var result = await _service.EvaluateAndTriggerAsync(boatPos, tripContext, new[] { candidateWithoutCoords });

        result.ShouldTrigger.Should().BeFalse();
        result.Reason.Should().Be(TriggerDecisionReason.PoiCoordinatesMissing);
    }

    [Fact]
    public async Task EvaluateAndTrigger_NullArguments_ThrowsArgumentNullException()
    {
        var pos = CreateBoatPosition(BachDangLat, BachDangLng);
        var context = CreateValidTripContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.EvaluateAndTriggerAsync(null!, context, new[] { CreateValidPoiCandidate() }));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.EvaluateAndTriggerAsync(pos, null!, new[] { CreateValidPoiCandidate() }));
    }
}
