# Luồng chuyến đi trên tàu: từ lúc lên tàu đến khi kết thúc chuyến

Tài liệu bàn giao cho người làm tiếp. Phạm vi: **sau khi khách đã mua vé và Staff đã quét QR cho lên tàu** → Captain khởi hành → tàu chạy
(giả lập GPS) và phát audio → Captain kết thúc chuyến. Phần mua vé và quét QR của Staff làm riêng (xem BR v2.0 và `docs/`).
Cách chạy demo từng bước: `docs/DEMO_AUDIO.md`. Hợp đồng sự kiện SignalR: `docs/REALTIME_CONTRACT.md`. Chi tiết audio: `docs/SIGHTSEEING_AUDIO_PLAYBACK.md`.

## 1. Bức tranh tổng thể
```
Staff quét xong ─► Captain: Mở đón khách ─► Khởi hành ─► [simulator chạy + audio phát] ─► Đến bến cuối ─► Kết thúc chuyến
                   (Boarding)               (EnRoute)                                       (đứng yên ở bến cuối)   (Completed, dừng hẳn)
```
Trạng thái chuyến (`Trip.Status`): `Scheduled → Boarding → EnRoute → Completed`. Mỗi lần đổi trạng thái phải ghi một `TripOperationEvent`
(quy tắc chung của dự án). Incident không tự đổi trạng thái; Admin quyết định.

## 2. Các bước và quy tắc nút của Captain
Trang Captain (`/captain`, `pages/captain/CaptainPortalPage.tsx`) chỉ hiện **chuyến hiện tại của tàu** (Captain gắn cố định với tàu, Admin phân tàu
cho chuyến), không có ô chọn chuyến. Backend là nơi quyết định nút nào được bấm; FE chỉ hiển thị theo `captainActions`.

| Bước | Nút | Điều kiện được bấm | Trạng thái sau | Nếu vi phạm |
|---|---|---|---|---|
| 1 | Mở đón khách | Chuyến `Scheduled`, **từ N phút trước giờ khởi hành** (mặc định 30) | `Boarding` | 409, lý do `NotYet` |
| 2 | Khởi hành | Đang `Boarding` (phải mở đón khách trước) **và** Admin đã điểm danh Captain cho chuyến | `EnRoute` | 409, `OpenBoardingFirst`; hoặc báo chưa điểm danh |
| 3 | Đến bến cuối | Đang `EnRoute`, có GPS còn mới, tàu cách bến cuối ≤ bán kính (150 m); với tuyến bến đầu = bến cuối, tàu phải từng đi xa bến đầu ≥ 500 m | giữ `EnRoute` (đánh dấu tới bến) | `NoPosition`, `NotAtLastStop`, `JourneyNotDone` |
| 4 | Kết thúc chuyến | Sau khi đã đến bến cuối | `Completed` | `WrongStatus` |

Ngoài ra có **SOS** (`POST /api/v1/trips/{tripId}/sos`, ghi chú tối đa 500 ký tự) có thể bấm khi chuyến đang chạy.

- Mã nguồn luật: `Application/Features/Trips/CaptainOperationRules.cs`; cấu hình: `CaptainOperationOptions` (section `CaptainOperations`:
  `Enforce`, `OpenBoardingLeadMinutes`, `ArriveRadiusM`, `ArriveMinTravelM`, `PositionMaxAgeSeconds`).
- Điểm danh: `CaptainAttendanceController` (`GET /api/v1/trips/{tripId}/captain-attendance`) và `ICaptainOperationsService.EnsureStartAsync`.
  Ở Development, seed điểm danh sẵn cho hai Captain demo; môi trường thật do Admin xác nhận.
- API: `GET /api/v1/trips/operations` (trả `captainActions` kèm lý do cho từng nút), `POST /api/v1/trips/{tripId}/status`,
  `POST /api/v1/trips/{tripId}/stops/{tripStopCallId}/progress`.
- Admin không bị áp các luật nút này.

## 3. Simulator GPS (giả lập tàu chạy)
`Infrastructure/BackgroundJobs/BoatSimulatorService.cs` + `Services/Tracking/DemoTimeline.cs` + `DemoRoutePaths.cs` (đường sông của tuyến).

- **Chỉ bắt đầu chạy khi chuyến chuyển sang `EnRoute`** (Captain bấm Khởi hành). Trước đó (`Scheduled`/`Boarding`) tàu đứng yên ở bến đầu;
  đồng hồ của chuyến tính từ lúc chuyển sang `EnRoute`.
- Chạy **một lượt duy nhất** `DemoTripSeconds` giây (210 giây ≈ 3,5 phút), dừng `DemoDwellSeconds` giây ở mỗi bến, chậm lại khi gần bến.
  Hết giờ mà Captain chưa bấm thì tàu **đứng ở bến cuối**, không quay lại chạy vòng.
- Sau khi `Completed`, chuyến không còn được giả lập; tàu không chạy lại.
- Mỗi tàu chỉ giả lập một chuyến; ưu tiên chuyến `EnRoute`.
- Cấu hình (biến môi trường, đặt trong `.env`, không commit):
  ```
  TRACKING_SIMULATOR_ENABLED=true
  TRACKING_SIMULATOR_INTERVAL_SECONDS=1
  TRACKING_SIMULATOR_INCLUDE_SCHEDULED=true
  TRACKING_SIMULATOR_DEMO_TRIP_SECONDS=210
  TRACKING_SIMULATOR_DEMO_DWELL_SECONDS=8
  ```
  Thiếu `DEMO_TRIP_SECONDS` (mặc định 0) thì tàu chạy theo thời lượng thật của chuyến nên rất chậm. Sau khi sửa `.env`:
  `docker compose up -d --force-recreate api`.
- Vị trí đi qua `BoatTrackingService` → cache Redis + SignalR `/hubs/boat-tracking` (`boatPosition`) → bản đồ Captain và Radar Admin.

## 4. Audio khi tàu di chuyển
Backend quyết định, trang Captain chỉ phát (giả lập loa trên tàu).
```
GPS → BoatTrackingService → TrackingAudioTriggerService
        ├─ StationAnnouncementDecisionService  (sắp tới / đã tới / rời bến, theo TripStopCall.VisitOrder)
        └─ PoiAudioTriggerDecisionService      (địa danh, chỉ chuyến tham quan, xét lần lượt theo thứ tự tuyến)
   → SignalROnboardAudioDispatcher → hub /hubs/onboard-audio (audioTrigger v2)
   → trình phát ngầm trên trang Captain: ACK → tải clip → phát vi rồi en → báo trạng thái
```
- Clip MP3 lưu trong bảng `AudioClips` (mỗi dòng một ngôn ngữ), nạp khi seed từ `SeedData/Audio`; sinh bằng `tools/audio/generate_audio.py`.
  Không cần Internet hay API key khi chạy.
- Chống phát trùng: sổ cái `AudioPlaybackRequests` (UNIQUE TripId + RoutePoiKey + VisitKey). Mỗi tàu một trình phát đang hoạt động
  (**kết nối mới nhất thắng**); thông báo quá hạn 90 s bị bỏ.
- Trình duyệt chỉ cho phát tiếng sau một cú chạm: trang tự mở loa ở lần chạm đầu tiên.
- Chuyến tham quan có **2 tuyến một chiều**, không dừng bến giữa: SS01 Bạch Đằng → Linh Đông, SS02 ngược lại; đi qua 5 địa danh
  (Landmark 81, Cầu Sài Gòn, Thảo Điền, Thanh Đa, Bình Quới). Chuyến thường: thông báo từng bến (bến đầu "rời bến", bến giữa
  "sắp tới/đã tới/rời bến", bến cuối "sắp tới/đã tới").

## 5. Dữ liệu demo (Development)
- `SWB-01`: tàu tham quan, Captain `captain@waterbus.vn`. `SWB-02`: tàu chuyến thường, Captain `captain2@waterbus.vn`. Mật khẩu `Captain@123456!`.
- Chuyến muốn demo phải thuộc **đúng tàu của Captain đang đăng nhập** và còn trong khung 30 phút trước giờ khởi hành (nếu không, nút Mở đón khách bị khoá).
  Ở Development seed điểm danh sẵn cho các chuyến trong 4 ngày tới; chuyến sinh sau lần khởi động API cần khởi động lại API để điểm danh bổ sung.

## 6. Cách kiểm tra bằng tay (toàn luồng)
1. Mua vé cho chuyến của tàu SWB-01 hoặc SWB-02, Staff quét QR (phần của luồng trước).
2. Đăng nhập Captain tương ứng, mở `/captain`, chạm một lần vào trang.
3. Mở đón khách → Khởi hành. Mở Radar Admin (`admin@waterbus.vn`, "Điều hành trực tiếp") để thấy tàu chạy.
4. Nghe audio theo vị trí; khi tàu tới bến cuối bấm Đến bến cuối → Kết thúc chuyến. Tàu dừng, không chạy lại.

## 7. Việc còn lại / điểm cần lưu ý cho người làm tiếp
- Nối với luồng trước: chuyến Captain thấy phải là chuyến có khách đã `CheckedIn` (Staff quét xong). Hiện luật nút **không** kiểm tra có khách lên tàu hay chưa; nếu cần ràng buộc đó, đây là chỗ thêm (`CaptainOperationRules` / `TripService`), phải đúng BR v2.0.
- Sau `Completed` nên chốt các vé không lên tàu (no-show) và hoàn tất chuyến theo BR; xem job `noshow-finalization` để tránh làm trùng.
- Chưa có màn hình Admin để xác nhận điểm danh Captain (chỉ có API).
- Chỉ một trình phát nhận audio mỗi tàu; SignalR chạy một instance.
- Bến/địa danh Admin thêm sau không có clip nên không phát (chỉ ghi log).
- Chưa kiểm: các test cần SQL thật (`WATERBUS_TEST_SQLSERVER`) và nghe tiếng thật trên trình duyệt trong phiên phát triển.
