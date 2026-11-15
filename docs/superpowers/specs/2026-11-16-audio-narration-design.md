# Thiết kế: Thông báo & thuyết minh audio trên tàu (Audio Narration)

Nhánh: `feature/audio-narration` · Trạng thái: **đã duyệt (2026-11-16)** · Nguồn chuẩn: BR v2.0 (BR-EXP-01..07, BR-TRK-01, BR-ROUTE-03, BR-INC-05)

## 1. Mục đích
Demo trước giảng viên: tàu giả lập chạy trên sông, **Màn hình Captain trên tàu tự động phát audio đúng lúc, đúng vị trí**:
- **Tàu thường (Commuter):** "Sắp tới bến *{tên bến}*" → "Đã tới bến *{tên bến}*, mời quý khách lên xuống" → "Tàu rời bến *{tên bến}*".
- **Tàu tham quan (Sightseeing):** "rời bến" ở bến đầu; đi qua địa danh thì phát giới thiệu ngắn; sắp cập bến cuối thì thông báo như tàu thường.
- Mỗi thông báo phát **tiếng Việt trước, tiếng Anh sau**.

Audio phát chung qua loa tàu (BR-EXP-04, BR-ID-06), không phát trên điện thoại hành khách.

## 2. Mục tiêu / Không phải mục tiêu / Điều tránh
**Mục tiêu:** (1) đủ 3 thông báo bến cho tàu thường, có tên bến; (2) tàu tham quan có 2 tuyến riêng không bến giữa, phát audio địa danh + sắp tới bến cuối; (3) vi rồi en; (4) audio tạo sẵn, lưu trong DB, tự phát khi tới vị trí; (5) một chuyến demo 3–4 phút, audio ngắn; (6) giữ chống phát trùng, dừng khi SOS/Suspended.
**Không phải mục tiêu:** TTS thời gian thực lúc demo; giọng đọc dự phòng của trình duyệt (`speechSynthesis`); UI Admin quản lý clip; tự sinh audio khi Admin thêm bến mới; thiết bị cá nhân; offline playback (BR-EXP-07 không bắt buộc); hỗ trợ nhiều backend instance.
**Điều tránh (thất bại dù chạy được):** hai audio chồng nhau hoặc bị cắt giữa chừng; phải thao tác tay giữa chừng demo; phát lặp khi tải lại trang; Hub/FE tự quyết định nghiệp vụ.

## 3. Hiện trạng liên quan (đã đọc code)
- Backend đã có luồng audio POI: GPS → `TrackingAudioTriggerService` → `PoiAudioTriggerDecisionService` (geofence, chống trùng bằng bảng `AudioPlaybackRequests` UNIQUE `(TripId, RoutePoiKey, VisitKey)`) → `OnboardAudioHub` gửi `audioTrigger`, player ACK. Chi tiết: `docs/SIGHTSEEING_AUDIO_PLAYBACK.md`. Cờ `SightseeingAudio:EnableGpsTriggerEvaluation` mặc định tắt.
- **Chưa có Onboard Player** (không có client nào nhận `audioTrigger`). `CaptainPortalPage` đã có GPS share, Open boarding/Start/Arrived.
- `BoatSimulatorService` đã phát vị trí qua đúng `IBoatTrackingService` (Source="Simulated"), nội suy dọc `DemoRoutePaths`, thời gian chuyến lặp vòng theo giờ chạy thật (quá dài cho demo).
- `AudioGuide` chỉ tiếng Việt, trỏ `AudioAssetUri` (Blob/đường dẫn), file trong seed (`/audio/poi-01.mp3`) chưa tồn tại; Azure TTS chưa cấu hình.
- Seed sai: mọi chuyến khởi hành ≥17h trên RT01/RT02 (5 bến) bị gán Sightseeing, trái BR-ROUTE-03.

## 4. Các cách đã cân nhắc
| | Mô tả | Ưu | Nhược |
|---|---|---|---|
| **A (chọn)** | Backend quyết định (khoảng cách, chống trùng, SOS), Captain screen chỉ phát | Đúng BR + AGENTS, tái dùng luồng và test sẵn | Nhiều việc BE hơn |
| B | Màn hình Captain tự so vị trí để phát | Nhanh | Trùng khi reload, bỏ qua SOS/Suspended, trái "Hub không quyết định nghiệp vụ" |

Nguồn audio: file MP3 sinh sẵn bằng script, lưu **SQL (varbinary)** (theo yêu cầu "lưu DB"), không dùng Blob/Azure để demo không phụ thuộc dịch vụ ngoài.

## 5. Thiết kế (Cách A)

### 5.1 Dữ liệu
- **Bảng mới `AudioClips`** (entity kế thừa `BaseEntity`, `IEntityTypeConfiguration`, soft-delete): `Kind` (`StationApproaching|StationArrived|StationDeparted|Poi`), `StationId?` hoặc `PoiId?` (đúng một), `LanguageCode` (`vi|en`), `Text`, `ContentType` ("audio/mpeg"), `Data` (`varbinary(max)`), `DurationSeconds`, `IsPublished`. UNIQUE `(Kind, StationId, PoiId, LanguageCode)` lọc `!IsDeleted`. Migration `AddAudioClips`. Đây là thay đổi entity/EF config dùng chung → nêu rõ trong mô tả PR.
- `AudioGuide` cũ giữ nguyên (không gỡ), luồng trigger mới đọc từ `AudioClips`.
- Sổ cái `AudioPlaybackRequests` **tái dùng**: một dòng = một thông báo (không phải mỗi clip). Với sự kiện bến: `RoutePoiKey = TripStopCall.Id`, `VisitKey = "approaching|arrived|departed"`; sự kiện POI giữ như cũ. Có thể thêm cột phân loại nếu builder thấy cần (migration cùng `AddAudioClips`).

### 5.2 Sự kiện bến và điều kiện phát
- Chỉ phát khi Trip `EnRoute`, đúng Boat, vị trí không cũ quá `MaxPositionAgeSeconds`, không có Incident Safety mở (giữ nguyên rule hiện có).
- **Tàu thường**, theo `TripStopCall` (VisitOrder, **không** dùng `Station.OrderIndex`): bến đầu chỉ `Departed`; bến giữa `Approaching → Arrived → Departed`; bến cuối `Approaching → Arrived`.
  - `Approaching`: khoảng cách ≤ `ApproachRadiusM`, chưa tới bến.
  - `Arrived`: khoảng cách ≤ `ArriveRadiusM`.
  - `Departed`: đã `Arrived` và tàu rời xa quá `DepartRadiusM`.
  - Thứ tự bắt buộc: không phát `Arrived` nếu chưa `Approaching` hợp lệ trong cùng lượt ghé, v.v. (mỗi sự kiện chỉ một lần/lượt).
- **Tàu tham quan:** `Departed` ở bến đầu + audio POI (geofence `RoutePoi.TriggerRadiusM` như hiện có, vi→en) + `Approaching` và `Arrived` của bến cuối.
- Tên bến nằm trong chính clip (mỗi bến × mỗi sự kiện × mỗi ngôn ngữ có clip riêng).

### 5.3 Hợp đồng `audioTrigger` (nâng `version: 2`)
Thêm `kind`, `subjectName` (tên bến/địa danh) và `clips: [{ language, playbackPath, durationSeconds, text }]` có thứ tự `vi` rồi `en`, thay cho `playbackPath` đơn. `requestId` vẫn là khoá khử trùng và ACK. REST phát file: `GET /api/v1/onboard-audio/requests/{requestId}/audio?lang=vi|en` (JWT Captain đúng Boat, trả MP3 từ `Data`, Range, `no-store`). Cập nhật `docs/SIGHTSEEING_AUDIO_PLAYBACK.md` và `docs/REALTIME_CONTRACT.md` trước khi merge.

### 5.4 Giả lập demo
- Thêm `Tracking:Simulator:DemoTripSeconds` (mặc định demo 210 giây; 0 = hành vi cũ theo giờ thật). Tàu chạy lặp vòng.
- Tàu **dừng (dwell) tại mỗi bến** vài giây để có "đã tới bến, chờ khách lên xuống", và giảm tốc khi gần bến/địa danh để thông báo không trôi qua trong vài giây.
- Ràng buộc kỹ thuật cho builder: bán kính phải lớn hơn bước di chuyển giữa 2 lần GPS (`IntervalSeconds` × tốc độ giả lập); thông báo phải phát xong (hoặc xếp hàng) trước khi sự kiện kế tiếp đến. Giá trị cụ thể chỉnh theo lần chạy thật.
- Độ dài: mỗi clip bến ≤ ~6 giây/ngôn ngữ; mỗi clip POI ≤ ~10 giây/ngôn ngữ; các địa danh cách nhau đủ xa để không chồng.

### 5.5 Seed (sửa theo yêu cầu)
- Bỏ quy tắc "≥17h là Sightseeing" trên RT01/RT02 (đều Commuter); không gán POI cho RT01/RT02.
- Thêm 2 Route Sightseeing riêng, **2 bến**: Bạch Đằng → Linh Đông và Linh Đông → Bạch Đằng, mỗi tuyến có `RouteStop`, `Schedule`, `RoutePoi` (5 địa danh có sẵn, đảo thứ tự cho chiều ngược), Trip Sightseeing, và đường đi trong `DemoRoutePaths` (tái dùng/đảo đường của RT01).
- Seed **idempotent theo mã** (không chỉ "nếu bảng rỗng") để DB đã có dữ liệu cũng nhận tuyến mới; builder chọn cách an toàn, không xoá dữ liệu cũ.
- Nạp clip MP3 từ thư mục seed vào `AudioClips`.

### 5.6 Tạo audio
Script dev một lần `tools/audio/generate_audio.py` dùng **edge-tts** (miễn phí, không cần key; giọng `vi-VN-HoaiMyNeural` và `en-US-JennyNeural`), đọc danh sách văn bản (tiếng Việt + tiếng Anh) từ một file JSON, xuất MP3 vào thư mục seed đã commit. Seeder đọc MP3 vào DB; chạy `docker compose up` không cần Internet hay key. Cần mạng khi chạy script tạo audio. Bến/địa danh Admin thêm sau không có clip: không phát (ghi log), không lỗi.

### 5.7 Onboard Player trên Captain Portal
Panel trong `CaptainPortalPage` (feature `features/captain/`): kết nối `/hubs/onboard-audio` (JWT `access_token`, auto reconnect, `GetState` sau reconnect); nút **"Bật loa"** (trình duyệt cần một cú chạm để cho phép tự phát); nhận `audioTrigger` → ACK `AcceptPlayback` trong 3 giây → tải từng clip kèm Bearer → phát **tuần tự vi rồi en**, hàng đợi (không chồng) → `ReportPlaybackState` Started/Completed/Failed; khử trùng theo `requestId`; `audioControl: pause` dừng ngay, `recheck` thì gọi `GetState`. Hiển thị dòng "Đang phát: *{subjectName}* (vi/en)" và nhật ký ngắn để người xem demo thấy. Không tải được clip thì báo `Failed` và ghi nhật ký; **không** có giọng đọc dự phòng của trình duyệt (đã quyết định bỏ ở giai đoạn này). Chuỗi UI qua `t()`.

### 5.8 Cấu hình demo
`SightseeingAudio:EnableGpsTriggerEvaluation=true`, `Tracking:Simulator:Enabled=true`, `DemoTripSeconds`, các bán kính — đặt trong cấu hình/`docker-compose` và cập nhật `appsettings.example.json` + `.env.example`. Luồng demo: Captain đăng nhập → "Bật loa" → Open boarding → Start trip → nghe audio chạy theo tàu.

## 6. Kiểm thử và nghiệm thu
- Unit test (đường chính + 1 lỗi + 1 biên mỗi rule): quyết định sự kiện bến (đủ thứ tự, mỗi lần một lượt, bến đầu/giữa/cuối, gating Trip/SOS/stale), tàu tham quan (POI + bến cuối, không phát bến đầu), simulator (dwell, `DemoTripSeconds`), seed (2 tuyến tham quan 2 bến, idempotent), endpoint audio `lang`.
- FE (vitest): hàng đợi vi→en, khử trùng `requestId`, pause/recheck, clip lỗi thì báo `Failed` và chuyển sang thông báo kế tiếp.
- Nghiệm thu thật: chạy `docker compose up -d --build`, đăng nhập Captain, chạy trọn một chuyến thường (5 bến) và một chuyến tham quan; kiểm tra nghe đúng thứ tự/nội dung, không chồng, không phát lặp khi reload trang; dừng khi báo SOS. Báo trung thực những gì chưa chạy được.

## 7. Để builder quyết định (đã đánh dấu là lựa chọn của tôi)
Giá trị bán kính/tốc độ/thời gian dwell cụ thể; cột phân loại thêm trên `AudioPlaybackRequests` hay không; cách seed idempotent; nội dung câu chữ cụ thể của các thông báo (ngắn gọn, tự nhiên); cấu trúc file JSON văn bản.
