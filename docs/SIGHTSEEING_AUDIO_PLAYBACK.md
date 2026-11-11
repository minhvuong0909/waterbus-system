# Sightseeing Audio — hợp đồng Backend → Onboard Player (BR-EXP-01..07, BR-ID-06, BR-INC-05)

BR: audio Sightseeing **phát chung qua loa Boat**, do **Onboard Player trên tàu** phát; Backend **không** tự phát ra loa và **không** dùng Ticket/TripAccess để mở khoá audio. Tài liệu này định nghĩa phần Backend làm và phần client (Onboard Player) phải làm.

## 1. Luồng

```
Captain GPS  ──REST/Hub──▶ BoatTrackingService ──(lọc stale)──▶ TrackingAudioTriggerService
                                                              │ Trip Sightseeing + EnRoute + tàu đúng + không có SOS mở
                                                              ▼
                                         PoiAudioTriggerDecisionService (geofence NTS, AudioGuide "vi" đã publish)
                                                              │ TryReserve (SQL UNIQUE Trip+RoutePoi+Visit)
                                                              ▼
                                         SignalROnboardAudioDispatcher ──audioTrigger──▶ OnboardAudioHub group onboard-audio:{boatId}
                                                              ▲                                      │ AcceptPlayback(requestId)
                                                              └───────────── chờ ACK ≤ AckTimeoutSeconds ◀┘
                                         sổ cái AudioPlaybackRequests: Reserved → Accepted → Started → Completed
```

## 2. Điều kiện được phát (Backend quyết định)
- Trip `Sightseeing`, `Status = EnRoute`, `BoatId` khớp vị trí; Trip `Suspended/Terminated/...` → không phát (BR-INC-05/EXP-06). Có Incident `Safety` đang `Open` → không phát (SOS chặn dù Admin chưa đổi trạng thái).
- Vị trí GPS không cũ hơn `SightseeingAudio:MaxPositionAgeSeconds` (mặc định 30; `StalePosition`). Mẫu cũ hơn mẫu đã nhận bị tracking loại trước đó (không giật lùi).
- Bán kính `RoutePoi.TriggerRadiusM` (không tự đặt mặc định); POI/RoutePoi active, có toạ độ; chỉ `AudioGuide` `vi` **đã publish** có định danh ổn định (không SAS/đường dẫn cục bộ).
- Chống phát trùng bền vững (BR-EXP-05): UNIQUE `(TripId, RoutePoiKey, VisitKey)` trong `AudioPlaybackRequests`; vào lại vùng/GPS lặp/nhiều instance **không** phát lại. Hành trình vòng dùng `VisitKey` riêng (RoutePoi khác nhau cho cùng POI vẫn tách biệt).
- `SightseeingAudio:EnableGpsTriggerEvaluation` mặc định **false** (bật có chủ đích).

## 3. Hợp đồng Onboard Player

**Xác thực**: JWT của tài khoản **Captain được gán cho Boat** (`Boat.CaptainUserId`). BR không định nghĩa danh tính thiết bị riêng; nếu có, đổi cơ chế ở `IOnboardAudioService.ResolveBoatAsync`. Boat được suy ra từ tài khoản — client **không** chọn Boat nên không thể nghe tàu khác. Passenger/Guest/TripAccess/Staff/Admin bị từ chối.

### Hub `/hubs/onboard-audio` (JWT qua `?access_token=`)
| Server → client | Payload |
|---|---|
| `audioState` (ngay khi kết nối) | `{ boatId, tripId?, tripStatus?, tripType?, playbackAllowed, reason }` |
| `audioTrigger` | `{ version:1, requestId, tripId, boatId, routePoiId, poiId, poiName, audioGuideId, playbackPath, contentType:"audio/mpeg", durationSeconds?, distanceMeters, triggeredAt }` |
| `audioControl` | `{ version:1, action:"pause"|"recheck", tripId?, reason, at }` — pause khi chuyến rời `EnRoute` hoặc có SOS an toàn; `recheck` khi chuyến về `EnRoute`: client phải gọi `GetState` và chỉ tiếp tục khi `playbackAllowed=true` (Backend không ra lệnh resume vì còn điều kiện SOS/loại chuyến) |

| Client → server | Ý nghĩa |
|---|---|
| `AcceptPlayback(requestId)` → bool | ACK: đã nhận và chấp nhận xếp lệnh (chưa chắc đã phát). Backend ghi `Accepted` vào SQL **trước** khi báo cho dispatcher, nên `ReportPlaybackState(Started)` ngay sau ACK luôn hợp lệ. Idempotent: ACK lặp cho lệnh đã `Accepted/Started/Completed` trả `true` mà không lùi trạng thái; lệnh `Released/Failed` trả `false` (không hồi sinh). Chỉ player **hoạt động** mới ACK được (xem bên dưới) |
| `ReportPlaybackState(requestId, state, message?)` → bool | `Started` \| `Completed` \| `Failed`; true nếu áp dụng |
| `GetState()` | Đồng bộ trạng thái (dùng sau reconnect) |

### REST (`Authorize: Captain`)
- `GET /api/v1/onboard-audio/state`
- `GET /api/v1/onboard-audio/requests/{requestId}/audio` — MP3 (`audio/mpeg`, hỗ trợ Range, `Cache-Control: private, no-store`). **Đây là "URL phát có phân quyền"**: `playbackPath` là đường dẫn tương đối, cần JWT Captain đúng Boat; không có URL Blob/SAS công khai. 404 nếu lệnh không thuộc Boat; 409 nếu chuyến không còn được phép phát hoặc lệnh đã thất bại; 409 khi kho lưu trữ lỗi.
- `POST .../requests/{requestId}/accept`, `POST .../requests/{requestId}/state` `{ state, message? }` (tương đương hub).

### Quy tắc client (dependency còn lại — Onboard Player chưa có trong repo)
1. Khử trùng theo `requestId` (retry cùng lượt ghé dùng **cùng** Id). Nhận lại Id đã ACK → ACK lại, **không** phát lại.
2. Phải gọi `AcceptPlayback` trong `AckTimeoutSeconds` (mặc định 3s) — nếu không Backend coi chưa nhận và gửi lại ở lần GPS kế tiếp (tối đa `MaxDispatchAttempts`, mặc định 3 lần gửi/lượt ghé).
3. Tự tải MP3 từ `playbackPath` kèm `Authorization: Bearer`; sau reconnect gọi `GetState` — `playbackAllowed=false` thì không phát.
4. Nhận `audioControl: pause` phải dừng/nhường ngay (Captain & thông báo an toàn ưu tiên — BR-EXP-06). Backend không điều khiển loa.
5. Báo `Started`/`Completed`/`Failed` để có audit; mất kết nối không bắt buộc phát tiếp (BR-EXP-07).

## 4. Lỗi & phục hồi
| Tình huống | Hành vi |
|---|---|
| Player offline khi tàu vào vùng | Không gửi; lượt ghé được nhả **không tiêu hao lượt thử**; player kết nối lại trong lúc tàu còn trong vùng sẽ nhận ở lần GPS kế tiếp |
| Asset không tồn tại / kho Blob lỗi | Không gửi, nhả lượt ghé, thử lại ở lần GPS sau; không tiêu hao lượt thử |
| Không ACK trong timeout | Nhả (`Released`), gửi lại cùng `requestId`; hết `MaxDispatchAttempts` → `Failed`, không tự phát lại |
| Player báo `Failed` | Còn lượt → `Released`, hết lượt → `Failed` |
| Instance chết khi đang giữ chỗ | Lease `ReservationLeaseSeconds` (30s) hết hạn → giành lại |
| Backend restart | Dedup nằm trong SQL nên không phát lại lượt đã ACK |

## 5. Giới hạn đã biết
- **Một player hoạt động mỗi Boat**: nhiều kết nối Captain cùng Boat được phép (reconnect/chuyển thiết bị), nhưng `audioTrigger` chỉ gửi tới kết nối **mới nhất còn hợp lệ**; kết nối cũ ở chế độ chờ (vẫn nhận `audioState`/`audioControl` pause|recheck, `AcceptPlayback` từ kết nối chờ trả `false`). Tránh hai loa/phiên cùng phát một lệnh. Cách ly giữa các Boat không đổi.
- **Fencing theo epoch**: mỗi lần reserve/giành lại lease, `AudioPlaybackRequests.ReservationEpoch` tăng 1 (compare-and-swap theo epoch quan sát được nên hai worker cùng giành lại chỉ một bên thắng). `RecordDispatch`, `MarkAccepted` (của evaluation) và `Release` đều bắt buộc đúng epoch: evaluation đã mất lease không thể ghi lượt gửi, gửi lệnh, đánh dấu Accepted hay nhả reservation của chủ mới, kể cả khi dòng vẫn `Reserved` với `ReservedAt` mới. `RequestId` ngoài (ổn định), retry, `MaxDispatchAttempts` và ACK của player không đổi (ACK của player chuyển Reserved→Accepted không cần epoch: **cố ý** — ACK là mức request, mọi lần gửi trước đó của cùng RequestId mà player đã nhận đều được tính; chủ mới khi đó không gửi lại được vì dòng đã `Accepted`). `RecordDispatch` gia hạn lease (`ReservedAt = now`) để khoảng ghi nhận→gửi→ACK nằm trong một lease. **Giới hạn còn lại**: nếu tiến trình bị treo lâu hơn lease giữa lúc ghi nhận và `SendAsync` thì có thể gửi trùng `audioTrigger`; lớp bảo vệ cuối là player khử trùng theo `requestId` (hợp đồng client). `SqlPoiTriggerDeduplicationService` giữ epoch theo instance scoped và không được dùng đồng thời cho nhiều evaluation. Player được chọn lại sau bước kiểm tra kho; kết nối chỉ đăng ký (active) sau khi thiết lập thành công.
- **Giới hạn đã biết**: REST `/onboard-audio/.../accept|state` xác thực theo Captain của Boat, không gắn với một kết nối cụ thể nên chính sách "một player hoạt động" chỉ áp cho hub (dispatch/ACK/report qua hub); không phải ranh giới bảo mật (cùng danh tính Captain).
- Registry hiện diện/ACK nằm trong tiến trình; SignalR chạy **một instance** (chưa có backplane). Nhiều instance cần backplane (Redis) và đổi `OnboardPlayerRegistry`.
- Chưa có replay sự kiện đã bỏ lỡ ngoài cơ chế retry theo GPS; trạng thái hiện tại lấy qua `GetState`.
- Không có test E2E với dịch vụ TTS/Blob thật: test dùng kho giả và SignalR client giả lập player (không khẳng định đã phát ra loa).
- Migration mới: `AddAudioPlaybackRequests` (bảng `AudioPlaybackRequests`).
