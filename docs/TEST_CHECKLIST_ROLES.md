# Checklist test thủ công theo role: Admin, Captain, Staff

Chỉ gồm những gì **test tự động không kiểm được**: giao diện thật trên trình duyệt, thiết bị thật (camera, GPS, điện thoại), kết nối realtime giữa nhiều role, dịch vụ ngoài (VNPay, email, thời tiết, Azure) và cấu hình triển khai.
Phần logic nghiệp vụ đã có test tự động **không lặp lại ở đây** (xem mục 6 để biết cái gì đã được phủ và ở đâu).

Cách dùng: làm theo "Cách làm", so với "Mong đợi", đánh dấu `[x]`. Không đạt thì ghi lỗi ngay dưới mục (ảnh chụp, giờ, mã chuyến/booking, `traceId` nếu là lỗi API).

---

## 0. Chuẩn bị

| # | Việc | Cách làm |
|---|---|---|
| 0.1 | Chạy hệ thống | `docker compose up -d --build`, web http://localhost:8080 |
| 0.2 | Bật Swagger (cho 0.5 đến 0.7) | `.env`: `ASPNETCORE_ENVIRONMENT=Development`, rồi `docker compose up -d`. Swagger: http://localhost:5000/swagger → **Authorize** → `Bearer <token>` |
| 0.3 | Lấy token | `POST /api/v1/auth/login` → lấy `token` và `userId` |
| 0.4 | Tài khoản seed | `admin@waterbus.vn`, `captain@waterbus.vn`, `staff@waterbus.vn` (mật khẩu trong `ApplicationDbContextInitializer.cs`). Khách vãng lai không cần tài khoản. **Chưa có tài khoản Passenger** (không có API đăng ký) |
| 0.5 | Gán Captain cho tàu (web chưa có ô chọn) | Token Admin → `GET /api/v1/boats` → `PUT /api/v1/boats/{id}` gửi đủ thông tin tàu + `"captainUserId": "<userId captain>"`. Seed đã gán sẵn 1 tàu |
| 0.6 | Đăng ký máy quét (web chưa có trang) | `POST /api/v1/admin/scanner-devices` `{ "deviceCode": "scanner-01", "label": "Máy bến Bạch Đằng" }` → lấy `id` |
| 0.7 | Phân công Staff vào điểm dừng (web chưa có trang) | `callId` từ `GET /api/v1/trips/{tripId}` (`stops[].callId`), rồi `POST /api/v1/checkins/assignments` `{ "deviceId": "...", "staffAccountId": "<userId staff>", "tripStopCallId": "<callId>", "isPrimaryOffline": true, "activeFrom": "<ISO hiện tại>", "activeUntil": null }` |
| 0.8 | App mobile kết nối API | API Docker chỉ mở `127.0.0.1:5000`, điện thoại không vào được. `waterbus-mobile/myApp/.env`: `EXPO_PUBLIC_API_BASE_URL=http://<IP LAN máy tính>:8080`, rồi `pnpm start`, mở bằng Expo Go |
| 0.9 | Nhiều role cùng lúc | Phiên đăng nhập lưu trong trình duyệt nên 2 tab cùng trình duyệt đè nhau. Admin: Chrome thường · Captain: Chrome ẩn danh · Khách: Edge |
| 0.10 | Thời tiết tự động (cho mục 2.3) | `.env`: `WEATHER_FAKE_WIND_MS=12` (≥10.6 là cảnh báo mức vừa; `WEATHER_FAKE_GUST_MS=20` là mức cao), `WEATHER_CRON=* * * * *` (mỗi phút). Phải có **chuyến khởi hành trong 3 giờ tới** (`WEATHER_LOOKAHEAD_HOURS`, mặc định 3), không có chuyến thì hệ thống không tạo cảnh báo. Chạy lại: `docker compose up -d` |

---

## 1. Admin: duyệt nhanh giao diện (mỗi trang mở một lần)

Logic CRUD, validate, xóa mềm, giá, ghế, vòng đời chuyến đã có test tự động. Ở đây chỉ kiểm giao diện hoạt động thật.



- [x] Mở lần lượt **Overview, Live Operations, Trips, Schedules, Routes, Stations, Boats, Seat classes, Fares, Incidents, Refunds, Check-in, Audio guides**: trang tải được, không lỗi trắng trang, không văn bản tiếng Anh lẫn tiếng Việt (đổi EN/VI thử một trang).
- [x] **Stations**: thêm bến mới trùng tọa độ một bến đã có → báo lỗi ở ô vĩ độ/kinh độ ("đã có một bến khác nằm đúng tọa độ này"). Bến cũ trùng tọa độ (vd ST06 "Thu Duc") thì sửa tọa độ hoặc ngừng hoạt động.
- [x] **Tạo ghế hàng loạt** (Boats → mở ghế của tàu → **Add seats**): tàu 60 ghế, chọn hạng + tiền tố + số ghế (mặc định điền sẵn toàn bộ chỗ còn lại), bấm **Add another class** để thêm hạng thứ hai, thứ ba; dòng tóm tắt hiện "Sẽ thêm 60 ghế (F01–F12, S01–S36, O01–O12)"; bấm một lần tạo đủ 60 ghế, sơ đồ ghế hiển thị đúng từng khoang, hàng mới cho mỗi khoang.
- [x] **Fares**: sửa giá một hạng ghế (nút Sửa), rồi đặt vé mới ở trang khách thấy giá mới; vé đã mua giữ giá cũ.
- [x] **Trips**: nút thao tác chỉ hiện những gì cho phép theo trạng thái chuyến (vd chuyến đang chạy không có nút Cancel).
- [x] **Check-in**: chọn chuyến, sửa giờ mở/đóng của một điểm dừng, lưu được; giờ đóng trước giờ mở báo lỗi.

## 2. Realtime và dịch vụ ngoài (chỉ kiểm bằng cách chạy thật)

### 2.1 Captain (web `/captain`, `captain@waterbus.vn`)
- [x] **GPS**: bấm Share GPS → trình duyệt hỏi quyền vị trí → cho phép → có vị trí; từ chối quyền thì hiện thông báo rõ ràng. Bấm Stop thì ngừng gửi. (GPS laptop có thể lệch xa sông, không phải lỗi.)
- [x] **Radar realtime**: trong lúc Captain gửi GPS, trang Live Operations của Admin (cửa sổ khác) thấy tàu hiện và **di chuyển không cần tải lại trang**; ngừng gửi thì tàu chuyển "Không có tín hiệu".
- [x] **SOS**: Captain bấm SOS, có hộp xác nhận, gửi xong; Admin thấy cảnh báo **ngay** (banner đỏ trên mọi trang Admin, mục 2.3).
- [x] **Tạm hoãn**: Admin Suspend chuyến → trang Captain tự cập nhật "chuyến đang tạm hoãn, chờ Admin" và khóa nút; Admin Resume → mở lại, không cần tải lại trang.
- [x] Captain mở `/admin` bị chặn; thấy đúng chuyến của tàu mình.
- [ ] **Narration** bật/tắt hiển thị đúng (nghe audio thật chỉ khi có khóa Azure, xem 2.5).

### 2.2 Staff (app mobile, `staff@waterbus.vn`, trên **điện thoại thật**)
Logic xác thực QR, khung giờ, quét trùng, sai bến, máy phụ, đồng bộ lô đã được kiểm bởi `pnpm test:scanner` (19 kịch bản) và test BE. Điện thoại thật cần kiểm:



- [x] Đăng nhập bằng Staff; tài khoản Admin/Captain bị từ chối.
- [x] **Camera**: cấp quyền, quét **QR vé thật** (từ "Vé của tôi" trên màn hình khác) và nhận đúng booking, đúng số vé.
- [x] **Offline thật**: tải trước manifest → bật chế độ máy bay → quét vé → vẫn xác thực được ("chữ ký số hợp lệ, xác thực offline") → tắt máy bay → đồng bộ → số vé chờ về 0 và trên web trạng thái vé là đã check-in.
- [x] Quét lại cùng vé, và quét QR bị sửa/ảnh chụp giả: app từ chối rõ ràng, không treo.
- [x] Màn **Phân công của tôi** hiện đúng điểm dừng đã giao (0.7); chưa giao thì hiện "chưa được phân công".
- [x] Giao diện không còn dữ liệu mẫu: trước demo **không** bấm "Mô phỏng quét QR vé mẫu" và "Sinh chuỗi QR hợp lệ" (nên ẩn các nút này).

### 2.3 Cảnh báo thời tiết tự động (Admin không cần bấm gì)
Hệ thống tự kiểm thời tiết theo lịch (mặc định mỗi 15 phút), tạo một cảnh báo cho mỗi tuyến và đẩy tới Admin. Nút "Check the weather now" chỉ là chạy ngay khi không muốn chờ.



- [x] Làm 0.10, mở một trang Admin **bất kỳ** (vd Stations), không bấm gì: trong vòng 1–2 phút xuất hiện **banner cảnh báo ở đầu trang** ("Cảnh báo đang mở: N", "Chuyến chờ quyết định: M") và **số đỏ cạnh mục Incidents** ở menu.
- [x] Bấm "Xem và xử lý" → Incidents → quyết định cho từng chuyến (Tạm hoãn/Hủy/Không xử lý) → số chuyến chờ quyết định giảm; đóng cảnh báo thì banner và số đỏ biến mất.
- [x] Chờ thêm một chu kỳ: không tạo cảnh báo trùng cho cùng tuyến.
- [x] Hạ gió về 3 (`WEATHER_FAKE_WIND_MS=3`), chạy lại API: cảnh báo cũ được tự đóng khi thời tiết bình thường.
- [x] **Dữ liệu thật** (khi có khóa): `WEATHER_PROVIDER=OpenWeatherMap`, `OPENWEATHERMAP_API_KEY=<khóa>`; hiện đúng tốc độ gió thực tế ở bảng thời tiết. Khóa sai/mạng lỗi thì chỉ ghi log, API không sập.

### 2.4 Thanh toán, email, CSV
- [x] **VNPay sandbox** đầu-cuối với IPN thật: cần tunnel công khai tới cổng 8080, `PUBLIC_URL` mới trong `.env`, IPN URL trên trang merchant. Đặt vé trên web, trả bằng thẻ NCB test, vé phát ra không cần can thiệp thủ công. (Nhánh tự đối soát `querydr` hiện trả 403 từ VNPay nên không dùng được.)
- [x] **Email** (Mailpit hoặc SMTP thật, xem `docker-compose.yml`): sau thanh toán nhận thư xác nhận kèm **ảnh QR hiển thị được** (thử cả Gmail lẫn Outlook); thư hủy chuyến; thư hoàn tiền ghi "2–3 ngày làm việc".
- [x] **CSV hoàn tiền** (Refunds → Tải CSV): mở bằng Excel, tiếng Việt không lỗi font, mỗi cột đúng; ô bắt đầu bằng `=`, `+`, `-`, `@` không bị Excel chạy thành công thức.

### 2.5 Audio thuyết minh
- [ ] Cần `AZURE_SPEECH_KEY`, `AZURE_SPEECH_REGION`, `AZURE_BLOB_CONNECTION_STRING`: Admin viết thuyết minh → **Generate and listen** nghe được giọng Việt → **Publish**. Thiếu khóa thì báo lỗi rõ ràng, không treo trang.
- [ ] Trên tàu: Captain bật Narration, tàu đến gần địa danh thì loa phát đúng audio đã xuất bản, chỉ một lần cho mỗi địa danh.

## 3. Kịch bản liên role (chạy đủ chuỗi một lần)

- [ ] **A.** Khách đặt vé + thanh toán → Admin thấy trong chuyến → Staff quét vé ở bến (điện thoại) → vé "đã check-in".
- [ ] **B.** Admin **hủy chuyến có vé** → khách vào "Vé của tôi" thấy vé hủy, nhận email hủy chuyến → Admin vào Refunds: tạo yêu cầu → tải CSV → xác nhận hoàn → khách nhận email hoàn tiền.
- [ ] **C.** Admin **chấm dứt chuyến giữa đường** sau khi Captain ghi nhận đã qua bến trung gian (chưa có nút trên trang Captain, dùng `POST /api/v1/trips/{id}/stops/{callId}/progress` ở Swagger): khách đã tới bến xuống không có trong danh sách hoàn, khách chưa tới thì hoàn đủ.
- [ ] **D.** Mất mạng một phút trên trang Live Operations rồi bật lại: trang tự kết nối lại, dữ liệu và banner cập nhật đúng (không đứng ở dữ liệu cũ).

## 4. Phân quyền: một lượt duyệt nhanh trên giao diện

Quy tắc phân quyền đã có test tự động ở cấp API; ở đây chỉ kiểm giao diện chuyển hướng đúng.



- [x] Khách chưa đăng nhập mở `/admin` và `/captain` → về trang đăng nhập.
- [x] Captain/Staff đăng nhập rồi mở `/admin/...` → bị chặn, không lộ dữ liệu.
- [x] Staff đăng nhập web (nếu có) không thấy menu Admin.

## 5. Triển khai

- [x] `docker compose up -d --build` từ máy sạch (không có volume cũ) chạy được: 4 container khỏe, đăng nhập Admin được, có dữ liệu seed.
- [ ] `/hangfire`: cần `HANGFIRE_DASHBOARD_USER/PASSWORD`; sai thông tin thì bị từ chối; đúng thì thấy các job `weather-guard`, `noshow-finalization`, `email-outbox` đang chạy.
- [ ] Đổi `PUBLIC_URL`/cổng: ReturnUrl của VNPay và CORS đổi theo.

## 6. Đã được test tự động, không cần kiểm tay

| Nội dung | Phủ bởi |
|---|---|
| Thêm/sửa/ngừng bến, tuyến, lịch, hạng ghế, giá, tàu, ghế; validate; xóa mềm; **bến trùng tọa độ** | `StationServiceTests`, `RouteAdminServiceTests`, `ScheduleAdminServiceTests`, `FareAdminServiceTests`, `BoatServiceTests`, `SeatInventoryTests`/`SeatInventorySqlTests` |
| Sinh chuyến từ lịch, chuyến lẻ, gán/đổi tàu, trùng lịch tàu | `TripPlanningServiceTests`, `ApiReliabilitySqlHttpTests` |
| Vòng đời chuyến (Suspend/Resume/Cancel/Terminate/Complete), bắt buộc lý do, nhật ký vận hành, SOS, ghi nhận bến trung gian | `TripLifecycleTests`, `TripServiceChangeStatusTests`, `TripServiceOperationsAndSosTests`, `TripServiceRecordStopProgressTests` |
| Weather Guard: ngưỡng gió, không tạo cảnh báo trùng, không tự đổi trạng thái chuyến, tự đóng, đồng thời, provider OpenWeatherMap | `WeatherAssessmentTests`, `WeatherGuardServiceTests`, `WeatherGuardConcurrencySqlTests`, `OpenWeatherMapProviderTests`, `IncidentService*Tests` |
| Quyết định của Admin cho từng chuyến bị ảnh hưởng, đóng cảnh báo | `IncidentServiceResolveTests`, `TripServiceChangeStatusTests` |
| Check-in: khung giờ, quét trùng, sai bến, phân công/thu hồi, máy chính/phụ, đồng bộ lô, no-show | `CheckInService*Tests`, `CheckInConcurrencySqlTests`, `pnpm test:scanner` (19 kịch bản mobile: xác thực RSA offline, đồng bộ theo ACK) |
| GPS: Captain chỉ gửi cho tàu của mình, bỏ điểm cũ, lịch sử thưa | `BoatTrackingServiceRecordTests`, `TrackingQueriesTests` |
| SignalR: nhóm, JWT qua query, khách chỉ đọc | `SeatSyncHubTests`, `SignalRPublishersTests`, `SignalRTokenReaderTests` |
| Hoàn tiền: điều kiện theo BR-RF, chống hoàn trùng, khứ hồi, NoShow, danh sách theo chuyến, CSV, xác nhận hàng loạt, 401/403 | `Refund*Tests`, `RefundManualModeSqlHttpTests`, `RefundCsvWriterTests` |
| Đăng nhập, rate limit đăng nhập, ProblemDetails 401/403 | `AuthServiceTests`, `AuthControllerRateLimitingTests`, `JwtProblemDetailsEventsTests` |
| Thanh toán VNPay: chữ ký, sai số tiền, replay, tiền về muộn | `VnPay*Tests`, `Payment*Tests` |
| Nội dung và hàng đợi email, mẫu thư | `NotificationTemplateTests`, `EmailOutbox*Tests`, `OutboxEnqueueTests` |
| Giao diện: form tạo ghế hàng loạt, banner cảnh báo, trang Refunds, đa ngôn ngữ | `vitest` (303 test FE) |

> Những test này chạy trong CI mỗi PR. Chúng **không** thay thế các mục ở trên (thiết bị thật, realtime nhiều cửa sổ, dịch vụ ngoài, cấu hình triển khai).
