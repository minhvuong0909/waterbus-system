# Audio Narration (thông báo bến + thuyết minh địa danh, vi→en) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tàu giả lập chạy một chuyến demo ~210 giây; Captain Portal tự phát audio vi→en đúng vị trí: "sắp tới / đã tới / rời bến {tên bến}" (tàu thường và tàu tham quan) và thuyết minh địa danh (tàu tham quan).

**Architecture:** Mở rộng luồng audio Sightseeing có sẵn (GPS → `TrackingAudioTriggerService` → decision service → `IOnboardAudioTriggerDispatcher` → `OnboardAudioHub` `audioTrigger` + sổ cái `AudioPlaybackRequests`). Backend quyết định; thêm service quyết định sự kiện bến, bảng `AudioClips` (MP3 trong SQL, mỗi clip một ngôn ngữ), `audioTrigger` v2 mang danh sách clip theo thứ tự. FE thêm Onboard Player trong `CaptainPortalPage`. Simulator thêm timeline demo (dwell + ease) và seed thêm 2 tuyến tham quan 2 bến.

**Tech Stack:** .NET 8, EF Core + SQL Server, SignalR, xUnit (test SQL thật qua `WATERBUS_TEST_SQLSERVER`), React + Vite + TS + vitest, Python `edge-tts` (script dev một lần).

**Spec:** `docs/superpowers/specs/2026-11-16-audio-narration-design.md`

## Global Constraints
- Quy tắc `AGENTS.md`: Controller→IService→Service→IRepository+IUnitOfWork; Service không inject `DbContext`; lỗi bằng exception; không `Remove()` entity nghiệp vụ (soft delete); không sửa migration đã merge; test mới trong `tests/WaterbusSystem.UnitTests/<Module>/`.
- Ghế/đoạn dùng `TripStopCall.VisitOrder`, **không** dùng `Station.OrderIndex`.
- Audio phát **chỉ trên Captain Portal** (giả lập loa tàu); không Ticket/TripAccess mở khoá audio (BR-ID-06).
- Không phát khi Trip không `EnRoute`, có Incident Safety mở, hoặc GPS cũ hơn `MaxPositionAgeSeconds` (30). Không phát trùng (UNIQUE `(TripId, RoutePoiKey, VisitKey)`).
- Mỗi thông báo = **2 clip: `vi` rồi `en`**, phát tuần tự, không chồng. Clip bến ≤ ~6 s/ngôn ngữ, clip địa danh ≤ ~10 s/ngôn ngữ.
- Chuyến demo: `Tracking:Simulator:DemoTripSeconds` = 210 (0 = hành vi cũ). Bán kính phải lớn hơn bước di chuyển giữa 2 GPS tick.
- Tàu thường: bến đầu chỉ `Departed`; bến giữa `Approaching→Arrived→Departed`; bến cuối `Approaching→Arrived`. Tàu tham quan: `Departed` bến đầu + POI + `Approaching`,`Arrived` bến cuối.
- Không giọng đọc dự phòng của trình duyệt; clip lỗi → báo `Failed`, ghi log, qua thông báo kế tiếp.
- Không commit secret; key cấu hình mới cập nhật `appsettings.example.json` + `.env.example` + `docker-compose.yml`. Chuỗi UI qua `t()` + `vi.ts`. FE dùng pnpm (máy dev: shim hỏng → chạy `node_modules/.bin/vitest.cmd`).
- PR phải nêu rõ: entity `AudioClip` + cột sổ cái + migration `AddAudioClips` (đụng EF config dùng chung) và hợp đồng `audioTrigger` v2.

## Review Focus
- Mất/đổi GPS giữa chừng (nhảy qua cả vùng `Arrive`): `Arrived` phải vẫn phát khi tàu đã tới gần bến theo thứ tự hợp lệ, không bị kẹt vĩnh viễn → test ở Task 5.
- Reload trang Captain giữa chuyến: không phát lại thông báo đã `Accepted/Started/Completed` → test ở Task 10.
- Hai kết nối Captain / Player offline khi tàu qua bến: không crash, không tiêu hao lượt thử, lượt ghé nhả để thử lại → test ở Task 3.
- Bến/địa danh thiếu clip (Admin thêm sau): bỏ qua + log, không lỗi, không chặn sự kiện sau → test ở Task 3.
- Chuyến quay lại (RT02/SS02, vòng lặp simulator): sự kiện chuyến sau không bị chặn bởi sổ cái chuyến trước (key theo `TripId`) và vòng lặp trong cùng Trip không phát lại → test ở Task 7.

## Cấu trúc file
| File | Việc |
|---|---|
| `Domain/Entities/AudioClip.cs`, `Domain/Enums/AudioClipKind.cs` (mới) | clip MP3 theo (Kind, subject, ngôn ngữ) |
| `Domain/Entities/AudioPlaybackRequest.cs` (sửa) | thêm `Kind`, `SubjectId` |
| `Infrastructure/Persistence/Configurations/EntityConfigurations.cs`, `ApplicationDbContext.cs`, migration `AddAudioClips` | EF |
| `Application/Interfaces/Repositories/IAudioClipRepository.cs` + `Infrastructure/Repositories/AudioClipRepository.cs` (mới) | đọc clip |
| `Application/Features/SightseeingAudio/Models/AudioTriggerRequest.cs`, `PoiGeofenceCandidate.cs` (sửa) | request v2 |
| `WebApi/Realtime/OnboardAudioMessages.cs`, `SignalROnboardAudioDispatcher.cs`, `Controllers/OnboardAudioController.cs` (sửa) | message v2, `?lang=` |
| `Application/Services/SightseeingAudio/OnboardAudioService.cs` (sửa) | đọc audio từ DB theo ngôn ngữ |
| `Application/Services/SightseeingAudio/StationAnnouncementDecisionService.cs` + `IStationAnnouncementDecisionService.cs` (mới) | sự kiện bến |
| `Application/Services/SightseeingAudio/TrackingAudioTriggerService.cs`, `PoiAudioTriggerDecisionService.cs` (sửa) | nối Commuter/Sightseeing, POI dùng clip |
| `Infrastructure/Services/Tracking/DemoTimeline.cs` (mới), `RoutePathInterpolator.cs`, `DemoRoutePaths.cs`, `BackgroundJobs/BoatSimulatorService.cs`, `Features/Tracking/TrackingOptions.cs` (sửa) | simulator demo |
| `Infrastructure/Persistence/ApplicationDbContextInitializer.cs` (sửa), `Persistence/SeedData/Audio/*.mp3`, `texts.json` (mới) | seed |
| `tools/audio/generate_audio.py` (mới) | sinh MP3 |
| `waterbus-fe/src/features/captain/...`, `pages/captain/CaptainPortalPage.tsx`, `constant/apiEndpoints.ts`, `i18n/dictionaries/vi.ts` | Onboard Player |
| `docker-compose.yml`, `.env.example`, `appsettings.example.json`, `docs/SIGHTSEEING_AUDIO_PLAYBACK.md`, `docs/REALTIME_CONTRACT.md` | cấu hình + tài liệu |

---

### Task 1: Entity `AudioClip` + cột sổ cái + migration

**Files:** Create `Domain/Entities/AudioClip.cs`, `Domain/Enums/AudioClipKind.cs`; Modify `Domain/Entities/AudioPlaybackRequest.cs`, `Infrastructure/Persistence/Configurations/EntityConfigurations.cs`, `ApplicationDbContext.cs`; Create migration `AddAudioClips`; Test `tests/WaterbusSystem.UnitTests/SightseeingAudio/AudioClipPersistenceSqlTests.cs` (theo mẫu các `*SqlTests` hiện có).

**Interfaces:**
- Produces: `enum AudioClipKind { StationApproaching = 1, StationArrived = 2, StationDeparted = 3, Poi = 4 }` (không đổi số sau này). `class AudioClip : BaseEntity { AudioClipKind Kind; Guid SubjectId; string LanguageCode; string Text; string ContentType; byte[] Data; int DurationSeconds; bool IsPublished }` — `SubjectId` = `StationId` (Kind bến) hoặc `PoiId` (Kind Poi). `AudioPlaybackRequest` thêm `AudioClipKind? Kind` và `Guid? SubjectId`. `ApplicationDbContext.AudioClips`.

- [ ] **Step 1: Failing test** `AudioClipPersistenceSqlTests`: (a) lưu 1 clip rồi đọc lại `Data` byte-identical; (b) chèn trùng `(Kind, SubjectId, LanguageCode)` chưa xoá mềm → `DbUpdateException`; (c) trùng nhưng bản cũ `IsDeleted=true` → được phép.
- [ ] **Step 2:** Chạy `cd waterbus-be && dotnet test --filter "FullyQualifiedName~AudioClipPersistence"` → FAIL (không biên dịch).
- [ ] **Step 3:** Thêm entity + enum; config `AudioClipConfiguration` trong `EntityConfigurations.cs` (`Data` `varbinary(max)`, `Text` max 500, `LanguageCode` max 5, unique filtered index `(Kind, SubjectId, LanguageCode) WHERE IsDeleted = 0`, `RowVersion.IsRowVersion()`); `AudioPlaybackRequest.Kind/SubjectId` nullable (dữ liệu cũ giữ nguyên). Tạo migration: `dotnet ef migrations add AddAudioClips --project src/WaterbusSystem.Infrastructure --startup-project src/WaterbusSystem.WebApi`.
- [ ] **Step 4:** Chạy lại test → PASS.
- [ ] **Step 5:** Commit `feat(audio): add AudioClip entity and playback ledger kind columns`.

### Task 2: `IAudioClipRepository` + REST phát audio theo ngôn ngữ

**Files:** Create `Application/Interfaces/Repositories/IAudioClipRepository.cs`, `Infrastructure/Repositories/AudioClipRepository.cs` (đăng ký trong `Infrastructure/DependencyInjection.cs`); Modify `OnboardAudioService.cs`, `IOnboardAudioService.cs`, `WebApi/Controllers/OnboardAudioController.cs`; Test `tests/.../SightseeingAudio/OnboardAudioServiceClipTests.cs`.

**Interfaces:**
- Produces: `record AudioClipRef(string Language, int DurationSeconds, string Text)`; `IAudioClipRepository { Task<IReadOnlyList<AudioClipRef>> ListPublishedAsync(AudioClipKind kind, Guid subjectId, CancellationToken ct); Task<StoredAudio?> ReadAsync(AudioClipKind kind, Guid subjectId, string language, CancellationToken ct); }` — `ListPublishedAsync` trả đúng thứ tự `vi` rồi `en`, chỉ clip `IsPublished && !IsDeleted`, và **rỗng nếu thiếu `vi`**. `PlaybackRequestView` thêm `AudioClipKind? Kind, Guid? SubjectId`. `IOnboardAudioService.GetAudioAsync(Guid captainId, Guid requestId, string language, CancellationToken ct)`. Endpoint `GET /api/v1/onboard-audio/requests/{requestId}/audio?lang=vi|en` (mặc định `vi`).
- `StoredAudio` là type sẵn có trả từ `IAudioStorageService.ReadAudioAsync`; dùng lại.

- [ ] **Step 1: Failing tests:** `GetAudio_ReturnsClipBytesForRequestedLanguage` (vi và en trả đúng bytes khác nhau); `GetAudio_UnknownLanguage_ThrowsNotFound` (`lang=fr`); `GetAudio_RequestOfOtherBoat_ThrowsNotFound`; `ListPublished_OrdersViThenEn_AndIsEmptyWithoutVi`.
- [ ] **Step 2:** Chạy `dotnet test --filter "FullyQualifiedName~OnboardAudioServiceClip"` → FAIL.
- [ ] **Step 3:** Cài repository (EF, `AsNoTracking`, lọc `!IsDeleted && IsPublished`); `GetAudioAsync` giữ nguyên các kiểm tra hiện có (Released/Failed → `ConcurrencyException`; `PlaybackAllowed` → bỏ điều kiện "Sightseeing" khi lệnh thuộc Kind bến, xem Task 6) rồi đọc bytes từ `IAudioClipRepository` thay cho `IAudioStorageService`; `lang` ngoài `vi|en` → `NotFoundException`. Controller nhận `[FromQuery] string lang = "vi"`.
- [ ] **Step 4:** Chạy test → PASS (+ test cũ của `OnboardAudio` vẫn pass; sửa các test cũ chỉ ở chỗ chữ ký `GetAudioAsync` đổi).
- [ ] **Step 5:** Commit `feat(audio): serve onboard audio from AudioClips by language`.

### Task 3: `audioTrigger` v2 (request model, dispatcher, hub message, ledger)

**Files:** Modify `Application/Features/SightseeingAudio/Models/AudioTriggerRequest.cs`, `WebApi/Realtime/OnboardAudioMessages.cs`, `WebApi/Realtime/SignalROnboardAudioDispatcher.cs`, `Infrastructure/Repositories/AudioPlaybackRepository.cs` (`RecordDispatchAsync`, `FindAsync` ghi/đọc `Kind`,`SubjectId`); Test `tests/.../SightseeingAudio/OnboardAudioDispatcherV2Tests.cs`.

**Interfaces:**
- Consumes: `IAudioClipRepository.ListPublishedAsync`, `AudioClipRef` (Task 2).
- Produces: `AudioTriggerRequest` thêm `AudioClipKind Kind`, `Guid SubjectId`, `string SubjectName`, `IReadOnlyList<AudioClipRef> Clips` và **bỏ** `AudioGuideId`/`AudioAssetUri` khỏi đường gửi (các property cũ giữ nếu test cũ cần, nhưng dispatcher không còn dùng). `AudioTriggerMessage` (version 2) = `{ RequestId, TripId, BoatId, Kind (string), SubjectId, SubjectName, Clips: [{ Language, PlaybackPath, DurationSeconds, Text }], DistanceMeters, TriggeredAt }`; `PlaybackPath` = `/api/v1/onboard-audio/requests/{requestId}/audio?lang={language}`. Dispatcher trả `AudioTriggerDispatchResult.Failed(requestId, "AudioAssetMissing")` khi `Clips` rỗng (thiếu clip), không gửi, không tiêu hao lượt thử (hành vi sẵn có của nhánh `AudioAssetMissing`).

- [ ] **Step 1: Failing tests:** `Dispatch_SendsV2MessageWithViThenEnClips`; `Dispatch_NoClips_FailsWithAudioAssetMissingAndDoesNotSend`; `Dispatch_PlayerOffline_FailsWithoutConsumingAttempt`; `RecordDispatch_PersistsKindAndSubjectId`.
- [ ] **Step 2:** Chạy `dotnet test --filter "FullyQualifiedName~OnboardAudioDispatcherV2"` → FAIL.
- [ ] **Step 3:** Đổi model + message + dispatcher (bỏ `storage.ExistsAsync`); repository ghi/đọc cột mới.
- [ ] **Step 4:** Chạy test → PASS; chạy toàn `FullyQualifiedName~SightseeingAudio` để các test dispatcher/hub cũ vẫn pass (cập nhật chúng theo model mới).
- [ ] **Step 5:** Commit `feat(audio): audioTrigger v2 carries ordered vi/en clips`.

### Task 4: POI dùng `AudioClips`

**Files:** Modify `PoiGeofenceCandidate.cs`, `AudioGuideCandidate.cs` (nếu chỉ còn POI dùng thì bỏ), `Infrastructure/Repositories/SightseeingAudioRepository.cs` (`GetTrackingSnapshotAsync`), `PoiAudioTriggerDecisionService.cs`; Test `tests/.../SightseeingAudio/PoiAudioTriggerDecisionClipTests.cs` (+ sửa test cũ).

**Interfaces:**
- Consumes: `IAudioClipRepository.ListPublishedAsync(AudioClipKind.Poi, poiId, ct)`.
- Produces: `PoiGeofenceCandidate.Clips : IReadOnlyList<AudioClipRef>` (rỗng = chưa có clip `vi`); decision service điền `Kind=Poi`, `SubjectId=PoiId`, `SubjectName=PoiName`, `Clips` vào `AudioTriggerRequest`; lý do skip khi `Clips` rỗng giữ `TriggerDecisionReason.NoPublishedVietnameseAudio`.

- [ ] **Step 1: Failing tests:** `Poi_WithViAndEnClips_TriggersWithBothClips`; `Poi_WithoutViClip_SkipsNoPublishedVietnameseAudio`; `Poi_AlreadyTriggeredForVisit_SkipsAsBefore`.
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~PoiAudioTriggerDecision"` → FAIL.
- [ ] **Step 3:** Snapshot nạp `Clips` cho từng candidate (một truy vấn gộp, không N+1); bỏ kiểm tra `IsStableAssetIdentifier`/AudioGuide trong decision service.
- [ ] **Step 4:** Test → PASS (toàn `SightseeingAudio` vẫn pass).
- [ ] **Step 5:** Commit `refactor(audio): POI trigger reads published clips`.

### Task 5: `StationAnnouncementDecisionService` (thuần, chưa nối GPS)

**Files:** Create `Application/Services/SightseeingAudio/IStationAnnouncementDecisionService.cs`, `StationAnnouncementDecisionService.cs`, `Application/Features/SightseeingAudio/Models/TripStationContext.cs`; Modify `SightseeingAudioOptions.cs`; Test `tests/.../SightseeingAudio/StationAnnouncementDecisionTests.cs`.

**Interfaces:**
- Consumes: `IGeoDistanceCalculator.CalculateDistanceMeters`, `IPoiTriggerDeduplicationService` (HasBeenTriggered/TryReserve/MarkAsTriggered/Release/GetReservationEpoch — `key = TripStopCallId`, `visitId = "approaching"|"arrived"|"departed"`), `IOnboardAudioTriggerDispatcher`, `AudioRequestIds.For(tripId, key, visitId)`.
- Produces: `record TripStopInfo(Guid TripStopCallId, Guid StationId, string StationName, int VisitOrder, decimal Latitude, decimal Longitude, IReadOnlyList<AudioClipRef> ApproachingClips, IReadOnlyList<AudioClipRef> ArrivedClips, IReadOnlyList<AudioClipRef> DepartedClips)`; `record TripStationContext(TripSightseeingContext Trip, IReadOnlyList<TripStopInfo> Stops)` (Stops sắp theo `VisitOrder`); `IStationAnnouncementDecisionService.EvaluateAndTriggerAsync(BoatPositionSnapshot position, TripStationContext context, CancellationToken ct) : Task<StationAnnouncementResult>` (`Triggered(kind, stopId)` | `Skipped(reason)`). `SightseeingAudioOptions` thêm `ApproachRadiusM = 500`, `ArriveRadiusM = 60`, `DepartRadiusM = 120` (giá trị khởi điểm cho demo; chỉnh ở Task 7).
- Quy tắc: mỗi lần gọi xét sự kiện kế tiếp chưa phát theo thứ tự `VisitOrder`: bến đầu → chỉ `Departed` (khi khoảng cách > `DepartRadiusM`); bến giữa/cuối → `Approaching` (≤ `ApproachRadiusM`), `Arrived` (≤ `ArriveRadiusM`, **hoặc** đã `Approaching` và tàu đã đi qua bến/tới gần hơn mọi mẫu trước – xem test biên), `Departed` (đã `Arrived` và > `DepartRadiusM`; bến cuối không có `Departed`). Mỗi lần gọi tối đa **một** lệnh. Điều kiện Trip/Safety/Boat giống `PoiAudioTriggerDecisionService`; `TripType` Commuter hoặc Sightseeing đều được xét ở đây; Sightseeing chỉ xét bến đầu (Departed) và bến cuối.

- [ ] **Step 1: Failing tests** (bố cục 3 bến A,B,C, mẫu vị trí dựng tay): `Commuter_FullRunThroughThreeStops_EmitsExpectedSequenceOnce` (A:Departed; B:Approaching,Arrived,Departed; C:Approaching,Arrived — đúng thứ tự, mỗi sự kiện một lần dù lặp mẫu); `Arrived_StillFires_WhenGpsJumpsPastTheArriveRadiusAfterApproaching` (mẫu 450 m rồi 20 m rồi 300 m hướng ra — không bị kẹt); `Sightseeing_OnlyOriginDepartedAndDestinationApproachingArrived`; `Skips_WhenTripNotEnRoute_OrSafetyActive_OrBoatMismatch`; `Skips_WhenStopHasNoViClip_AndDoesNotBlockLaterEvents`; `DispatchFailure_ReleasesReservationAndAllowsRetry`.
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~StationAnnouncementDecision"` → FAIL.
- [ ] **Step 3:** Cài service theo quy tắc trên, tái dùng đúng chuỗi reserve→dispatch→mark/release của `PoiAudioTriggerDecisionService` (trích hàm dùng chung nếu tránh trùng lặp hợp lý; nếu trích, đặt ở file riêng trong `Services/SightseeingAudio/`).
- [ ] **Step 4:** Test → PASS.
- [ ] **Step 5:** Commit `feat(audio): station announcement decision service`.

### Task 6: Snapshot bến + nối `TrackingAudioTriggerService`

**Files:** Modify `ISightseeingAudioRepository.cs`, `Infrastructure/Repositories/SightseeingAudioRepository.cs`, `TrackingAudioTriggerService.cs`, `Infrastructure/DependencyInjection.cs`, `OnboardAudioService.cs` (`GetStateAsync` reason `NotSightseeing` bỏ — Commuter được phép); Test `tests/.../SightseeingAudio/TrackingAudioTriggerStationTests.cs`, `SightseeingAudioRepositoryStationSnapshotSqlTests.cs`.

**Interfaces:**
- Consumes: `IStationAnnouncementDecisionService` (Task 5), `IAudioClipRepository` (Task 2).
- Produces: `ISightseeingAudioRepository.GetStationSnapshotAsync(Guid tripId, Guid boatId, CancellationToken ct) : Task<TripStationContext?>` (join `TripStopCall → RouteStop → Station`, bỏ call/trạm `IsDeleted`, kèm clip 3 sự kiện của từng bến); `TrackingAudioTriggerService.EvaluateAsync` trả `PoiAudioTriggerEvaluationResult?` như cũ (POI) **và** gọi service bến; trả kết quả POI nếu Sightseeing, bến chạy trước POI trong cùng tick khi có sự kiện bến (tối đa một lệnh/tick; POI xét ở tick sau).
- `GetStateAsync.playbackAllowed` = Trip `EnRoute` và không có SOS (mọi `TripType`).

- [ ] **Step 1: Failing tests:** `Commuter_EnRoute_RunsStationEvaluation_NotPoi`; `Sightseeing_EnRoute_StationEventTakesPriorityThenPoiNextTick`; `Stale_Position_Skips`; SQL: `StationSnapshot_OrdersByVisitOrder_AndCarriesClips`; `GetState_Commuter_IsAllowedWhenEnRoute`.
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~TrackingAudioTriggerStation|FullyQualifiedName~StationSnapshot"` → FAIL.
- [ ] **Step 3:** Cài snapshot (một truy vấn, `AsNoTracking`), nối service, đăng ký DI.
- [ ] **Step 4:** Test → PASS; chạy `dotnet test --filter "FullyQualifiedName~SightseeingAudio|FullyQualifiedName~Tracking"`.
- [ ] **Step 5:** Commit `feat(audio): evaluate station announcements from GPS for commuter and sightseeing`.

### Task 7: Simulator demo timeline

**Files:** Create `Infrastructure/Services/Tracking/DemoTimeline.cs`; Modify `RoutePathInterpolator.cs` (thêm `FractionOf`), `DemoRoutePaths.cs` (alias mã tuyến tham quan `SS01`→Rt01, `SS02`→Rt02), `Features/Tracking/TrackingOptions.cs`, `BackgroundJobs/BoatSimulatorService.cs`; Test `tests/.../Tracking/DemoTimelineTests.cs`.

**Interfaces:**
- Produces: `RoutePathInterpolator.FractionOf(IReadOnlyList<GeoPoint> path, GeoPoint point) : double` (tỉ lệ 0..1 điểm gần nhất trên đường); `static class DemoTimeline { public static (double Fraction, bool Dwelling) At(IReadOnlyList<double> stopFractions, double totalSeconds, double dwellSeconds, double elapsedSeconds); }` — `stopFractions` tăng dần gồm cả bến đầu (0) và cuối (1); chuyến = các chặng di chuyển (ease smoothstep trong từng chặng) xen dwell `dwellSeconds` ở mỗi bến giữa; `elapsedSeconds` được lấy mod `totalSeconds` (lặp vòng). `BoatSimulatorOptions` thêm `DemoTripSeconds = 0`, `DemoDwellSeconds = 8`.
- Simulator: khi `DemoTripSeconds > 0` thì vị trí = `PointAt(path, DemoTimeline.At(...).Fraction)` với `stopFractions` tính bằng `FractionOf` từ toạ độ bến (đã sắp theo `VisitOrder`), elapsed = `now - Start`; Trip Suspended giữ nguyên hành vi đứng yên; `DemoTripSeconds = 0` giữ hành vi cũ.

- [ ] **Step 1: Failing tests:** `At_StartsAtZeroAndEndsAtOne`; `At_HoldsExactStopFractionDuringDwell` (ở giữa dwell, `Fraction == stopFraction` và `Dwelling`); `At_IsMonotonicNonDecreasingWithinOneLoop`; `At_WrapsAroundAfterTotalSeconds`; `At_SlowsNearStops` (độ dời 1 s ngay sau bến < độ dời 1 s giữa chặng); `FractionOf_ReturnsStationPositionOnRt01Path` (5 bến seed ra phân số tăng dần).
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~DemoTimeline"` → FAIL.
- [ ] **Step 3:** Cài hàm + nối simulator.
- [ ] **Step 4:** Test → PASS. Thêm test mô phỏng: chạy 1 vòng RT01 bước 1 s qua `StationAnnouncementDecisionService` với bán kính mặc định → đúng chuỗi sự kiện Task 5 (kiểm bán kính ≥ bước tick); nếu fail, chỉnh `ApproachRadiusM/ArriveRadiusM/DepartRadiusM` ở `SightseeingAudioOptions` đến khi pass.
- [ ] **Step 5:** Commit `feat(tracking): demo timeline with dwell and easing for the GPS simulator`.

### Task 8: Seed 2 tuyến tham quan + sửa seed sai

**Files:** Modify `ApplicationDbContextInitializer.cs` (`SeedPointsOfInterestAsync`, `SeedDemoTripsForUpcomingDaysAsync`, thêm `SeedSightseeingRoutesAsync`); Test `tests/.../Persistence/SightseeingSeedTests.cs` (theo mẫu test initializer nếu có, nếu không dùng SQL test).

**Interfaces:**
- Produces: Route `SS01` "Bạch Đằng - Linh Đông (Tham quan)" và `SS02` "Linh Đông - Bạch Đằng (Tham quan)" (`ServiceType = "Sightseeing"`), mỗi tuyến đúng 2 `RouteStop` (đầu/cuối), có `Schedule` + `ScheduleStop`, `RoutePoi` cho 5 POI (SS02 đảo thứ tự `DisplayOrder`), trip hàng ngày `TripType.Sightseeing`; RT01/RT02 **không** còn `RoutePoi` và mọi trip của chúng là `Commuter` (bỏ quy tắc `≥17:00`). Seed idempotent **theo `Route.Code`** (`SS01`/`SS02`), không xoá dữ liệu cũ.

- [ ] **Step 1: Failing tests:** `Seed_CreatesTwoSightseeingRoutesWithTwoStopsEach`; `Seed_SightseeingRoutePois_AreOrderedAndReversedForSs02`; `Seed_Rt01Rt02Trips_AreAllCommuter`; `Seed_RunTwice_DoesNotDuplicate`; `Seed_EveryRoutePoi_IsWithinTriggerRadiusOfTheRoutePath` (khoảng cách từ mỗi POI tới đường `DemoRoutePaths.For("SS01")` ≤ `TriggerRadiusM`; nếu POI seed lệch, chỉnh `TriggerRadiusM`/toạ độ POI trong seed cho khớp — đây là dữ liệu demo).
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~SightseeingSeed"` → FAIL.
- [ ] **Step 3:** Cài seed. Đảm bảo tàu `SWB-01` (đã gán `captain@waterbus.vn`) có chuyến Sightseeing và chuyến Commuter gần hôm nay để demo.
- [ ] **Step 4:** Test → PASS.
- [ ] **Step 5:** Commit `fix(seed): dedicated sightseeing routes, commuter trips for RT01/RT02`.

### Task 9: Sinh MP3 (vi/en) + nạp clip vào DB khi seed

**Files:** Create `tools/audio/generate_audio.py`, `tools/audio/README.md` (ngắn: cách chạy), `waterbus-be/src/WaterbusSystem.Infrastructure/Persistence/SeedData/Audio/texts.json` và các `*.mp3` sinh ra; Modify `WaterbusSystem.Infrastructure.csproj` (copy `SeedData/Audio/**` ra output), `ApplicationDbContextInitializer.cs` (`SeedAudioClipsAsync`); Test `tests/.../Persistence/AudioClipSeedTests.cs`.

**Interfaces:**
- `texts.json`: danh sách `{ "key": "station:ST01:approaching", "vi": "...", "en": "..." }` cho 5 bến × 3 sự kiện (đủ để bến đầu dùng `departed`, bến cuối dùng `approaching/arrived`; vẫn sinh đủ 3 cho mọi bến) và `poi:<tên POI>` cho 5 POI. Câu có tên bến, ví dụ vi: "Sắp tới bến Linh Đông."; en: "Approaching Linh Dong pier." Độ dài chữ phải đọc ≤ ~6 s (bến) và ≤ ~10 s (POI).
- Script: `python tools/audio/generate_audio.py` → `pip install edge-tts` một lần; voice `vi-VN-HoaiMyNeural` và `en-US-JennyNeural`; ghi `<key>.<lang>.mp3` (dấu `:` thay `_`). Seeder đọc file theo `Station.Code`/`PointOfInterest.Name`, `DurationSeconds` đọc từ header MP3 hoặc ước lượng khi sinh (ghi vào `texts.json` do script cập nhật), idempotent theo `(Kind, SubjectId, LanguageCode)`.

- [ ] **Step 1: Failing tests:** `Seed_CreatesViAndEnClipsForEveryStationEventAndPoi` (5×3×2 + 5×2 = 40 clip, `Data` không rỗng, `IsPublished`); `Seed_RunTwice_DoesNotDuplicateClips`; `Seed_AllClipDurations_AreWithinLimits` (bến ≤ 7 s, POI ≤ 11 s, theo `DurationSeconds`).
- [ ] **Step 2:** `dotnet test --filter "FullyQualifiedName~AudioClipSeed"` → FAIL.
- [ ] **Step 3:** Viết `texts.json` (văn phong tự nhiên, ngắn) → chạy script (cần Internet) → kiểm tra nghe thử vài file → cài `SeedAudioClipsAsync` + `.csproj` copy.
- [ ] **Step 4:** Test → PASS.
- [ ] **Step 5:** Commit `feat(audio): vi/en seed clips and generator script`.

### Task 10: Onboard Player trên Captain Portal

**Files:** Create `waterbus-fe/src/features/captain/services/onboardAudioService.ts`, `features/captain/utils/audioQueue.ts`, `features/captain/hooks/useOnboardPlayer.ts`, `features/captain/components/OnboardPlayerPanel.tsx`; Modify `pages/captain/CaptainPortalPage.tsx`, `constant/apiEndpoints.ts` (`HUBS.onboardAudio = '/hubs/onboard-audio'`, `HUB_EVENTS.audioState|audioTrigger|audioControl`, REST `onboardAudio.*`), `i18n/dictionaries/vi.ts`; Tests `audioQueue.test.ts`, `OnboardPlayerPanel.test.tsx`, `onboardAudioService.test.ts`.

**Interfaces:**
- Produces: `interface AudioTriggerV2 { version: 2; requestId: string; tripId: string; boatId: string; kind: string; subjectId: string; subjectName: string; clips: { language: 'vi'|'en'; playbackPath: string; durationSeconds: number; text: string }[]; distanceMeters: number; triggeredAt: string }`; `createAudioQueue(): { enqueue(t: AudioTriggerV2): boolean /* false nếu requestId đã thấy */; next(): AudioTriggerV2 | undefined; clear(): void; seen(requestId: string): boolean }`; `useOnboardPlayer(enabled: boolean): { status: RealtimeStatus; nowPlaying?: { subjectName: string; language: string }; log: string[]; armed: boolean; arm(): void }`. Dùng `connectHub` (theo `useFleetHub.ts`) với handler `audioTrigger`/`audioControl`/`audioState`; fetch clip bằng `Authorization: Bearer` → `blob` → `Audio`; trình tự: nhận → `AcceptPlayback(requestId)` → (queue) → với từng clip: `ReportPlaybackState(requestId,'Started')` ở clip đầu, phát lần lượt `vi` rồi `en`, `Completed` sau clip cuối; lỗi tải/phát → `Failed`, qua thông báo kế. `audioControl: pause` dừng ngay + xoá hàng đợi; `recheck` gọi `GetState` và chỉ tiếp tục khi `playbackAllowed`. Sau reconnect gọi `GetState`. Nút "Bật loa" (`arm()`) mở khoá autoplay; chưa bật thì hiển thị cảnh báo và không `AcceptPlayback`.

- [ ] **Step 1: Failing tests:** `audioQueue`: `enqueue returns false for a requestId already seen`, `next returns triggers FIFO`, `clear drops pending but keeps seen ids` (reload không phát lại đã nhận); `OnboardPlayerPanel`: `shows the arm button and warning until armed`, `plays vi clip before en clip and reports Started then Completed` (mock `Audio`, mock hub), `a failing clip reports Failed and moves on to the next trigger`, `pause control stops playback and empties the queue`; `onboardAudioService`: `builds the clip url with the bearer token and lang`.
- [ ] **Step 2:** `cd waterbus-fe && node_modules/.bin/vitest.cmd run src/features/captain` → FAIL.
- [ ] **Step 3:** Cài các file; render `OnboardPlayerPanel` trong `CaptainPortalPage` (hiện `nowPlaying` dạng "Đang phát: {subjectName} ({vi|en})" + nhật ký 5 dòng cuối); chuỗi UI bằng `t()` và thêm bản dịch `vi.ts`.
- [ ] **Step 4:** Chạy `node_modules/.bin/vitest.cmd run`, `node_modules/.bin/eslint.cmd src`, `node_modules/.bin/tsc.cmd --noEmit`, `node_modules/.bin/vite.cmd build` → đều pass.
- [ ] **Step 5:** Commit `feat(captain): onboard audio player on the captain portal`.

### Task 11: Cấu hình demo, tài liệu, nghiệm thu thật

**Files:** Modify `docker-compose.yml`, `.env.example`, `waterbus-be/src/WaterbusSystem.WebApi/appsettings.example.json`, `docs/SIGHTSEEING_AUDIO_PLAYBACK.md`, `docs/REALTIME_CONTRACT.md`.

- [ ] **Step 1:** Thêm biến `SIGHTSEEING_AUDIO_APPROACH_RADIUS_M/ARRIVE/DEPART`, `TRACKING_SIMULATOR_DEMO_TRIP_SECONDS` (mặc định demo 210), `TRACKING_SIMULATOR_DEMO_DWELL_SECONDS` (8) vào `docker-compose.yml`/`.env.example`/`appsettings.example.json`; hướng dẫn bật demo (`SIGHTSEEING_AUDIO_ENABLE_GPS_TRIGGER_EVALUATION=true`, `TRACKING_SIMULATOR_ENABLED=true`, `TRACKING_SIMULATOR_INTERVAL_SECONDS=1`, `TRACKING_SIMULATOR_INCLUDE_SCHEDULED=true`).
- [ ] **Step 2:** Cập nhật `SIGHTSEEING_AUDIO_PLAYBACK.md` (điều kiện phát cho tàu thường, sự kiện bến, `AudioClips`, `lang`, Player nay có trong repo, giới hạn đã biết còn lại) và `REALTIME_CONTRACT.md` (`audioTrigger` v2: payload, `kind`, `clips`).
- [ ] **Step 3:** Chạy toàn bộ: `cd waterbus-be && dotnet build WaterbusSystem.sln && dotnet test WaterbusSystem.sln` (đặt `WATERBUS_TEST_SQLSERVER`); FE `lint`/`test`/`build`. Báo trung thực test nào fail/chưa chạy (đã biết: `CheckInConcurrencySqlTests.ConcurrentNonOverlappingFinitePrimaryAssignments_ShouldBothSucceed` fail trước thay đổi này).
- [ ] **Step 4:** Nghiệm thu thật: `docker compose up -d --build` (DB mới hoặc DB cũ — kiểm tra seed idempotent tạo `SS01/SS02` + 40 clip), đăng nhập `captain@waterbus.vn`, mở Captain Portal, "Bật loa", `Open boarding` → `Start trip`: nghe một chuyến Commuter đủ chuỗi sự kiện vi→en; lặp với chuyến Sightseeing (POI + bến cuối); reload trang giữa chuyến không phát lại; báo SOS → dừng. Ghi lại thời lượng thực của một vòng (~3–4 phút) và chỉnh bán kính nếu thiếu/trùng.
- [ ] **Step 5:** Commit `docs(audio): demo configuration and contract v2`.

## Self-review (đã chạy)
- **Phủ spec:** §5.1→T1; §5.2→T5/T6; §5.3→T2/T3; §5.4→T7; §5.5→T8; §5.6→T9; §5.7→T10; §5.8 + tài liệu→T11; kiểm thử/nghiệm thu→T11 và từng task. POI vi→en → T4. Không giọng dự phòng → Global Constraints + T10.
- **Chênh lệch có chủ ý so với spec:** spec §5.4 nói giảm tốc "khi gần bến/địa danh"; plan chỉ ease theo chặng giữa các bến (POI không giảm tốc vì hàng đợi của player đảm bảo không chồng). Spec §5.2 chưa nói thứ tự bến/POI trong cùng tick; plan chọn bến trước, POI tick sau.
- **Nhất quán kiểu:** `AudioClipRef(Language, DurationSeconds, Text)`, `AudioClipKind`, `TripStationContext`, `TripStopInfo`, `AudioTriggerV2` dùng đúng một tên xuyên Task 2–6 và 10.
- **Tỉ lệ:** các task chỉ nêu chữ ký, tên test, giá trị spec; thân hàm không viết sẵn.
