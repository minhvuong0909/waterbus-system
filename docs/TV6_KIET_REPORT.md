# Báo cáo TV6 — Kiệt (Real-time Hubs, Background Jobs, DevOps)

> Cập nhật 12/10/2026. Quy tắc code: `AGENTS.md`. Hợp đồng realtime cho FE: `docs/REALTIME_CONTRACT.md` (có trong PR #7, #8).

## 1. Tổng quan

| Hạng mục | Kết quả | PR |
|---|---|---|
| Conventions + file quy tắc cho AI agent (`AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `docs/CONVENTIONS.md`) | ✅ Đã merge | #3 |
| Docker: Dockerfile API (non-root) + Web (nginx, proxy `/api` `/hubs`), compose 4 service | ✅ Đã merge | #3 |
| G1 — CI GitHub Actions (BE build + test có SQL Server thật, FE build, Docker build) | ✅ Đã merge | #4 |
| G2 — Nền SignalR (2 hub, JWT qua query, CORS, publisher) | 🔵 Chờ review | #5 |
| Sửa lỗi seed `TripStopCall` khi khởi động lại | 🔵 Chờ review | #6 |
| G3 — BoatTrackingHub, REST tracking, giả lập GPS | 🔵 Chờ review (xếp chồng trên #5) | #7 |
| G4 — Hangfire + Weather Guard + API Incident | 🔵 Chờ review (xếp chồng trên #7) | #8 |
| G5 — SeatSyncHub: giới hạn kết nối ẩn danh | 🔵 Chờ review (xếp chồng trên #8) | #9 |
| Sửa rate limit đặt vé bị dùng chung IP nginx | 🔵 Chờ review | #10 |
| G6 — Deploy | ⏸ Hoãn: deploy lên AWS sau khi cả nhóm xong và test local đạt | — |

**Thứ tự merge:** #6 và #10 độc lập; **#5 → #7 → #8 → #9** (merge #5 xong, GitHub tự chuyển base #7 về `main`, cứ thế tiếp). PR xếp chồng chỉ chạy CI khi base đã là `main`.

**Test:** chạy trong container .NET 8, mọi nhánh chỉ còn đúng **4 test `CreateBookingCommandHandlerTests` đã biết là fail** (handler checkout cũ của TV2). Trên nhánh cuối (#9): 140 pass, 4 fail, 9 skip (test cần SQL thật; CI có chạy).

## 2. Chi tiết

### G1 — CI (`.github/workflows/ci.yml`)
- Chạy với PR vào `main`, push lên `main`, và chạy tay.
- Backend: restore, build Release, test, có SQL Server service để `DomainPersistenceTests` chạy thật.
- 4 test fail đã biết được loại **theo tên** trong `KNOWN_FAILING_TESTS_FILTER`. **TV2 xoá biến này khi refactor checkout xong.**

### G2 — Nền SignalR (#5)
- Hub `/hubs/boat-tracking` (bắt buộc đăng nhập; `JoinFleet` chỉ Admin) và `/hubs/seat-sync` (Guest chỉ đọc).
- Interface ở Application (`IBoatTrackingPublisher`, `ISeatEventPublisher`, `IOperationsAlertPublisher`); cài đặt dùng `IHubContext` đặt ở **WebApi** vì `Infrastructure` không tham chiếu ASP.NET Core.
- JWT đọc từ query `access_token` **chỉ** cho đường dẫn `/hubs`; CORS thêm `AllowCredentials`.

### Sửa seed (#6)
- Từ lần khởi động thứ 2, seed ném `DbUpdateConcurrencyException`, khiến Trip demo **không có `TripStopCall`**: không tìm/đặt vé theo đoạn và không giả lập GPS được.
- Nguyên nhân: thêm entity con qua navigation của Trip đang được track, với Guid Id gán sẵn → EF sinh `UPDATE` thay vì `INSERT`.
- Có test hồi quy (fail nếu bỏ phần sửa).

### G3 — Theo dõi vị trí tàu (#7)
- `RecordBoatPositionCommand`:
  - Chỉ Captain **được gán cho tàu** mới được gửi GPS (403 nếu sai).
  - Bỏ qua điểm cũ hơn vị trí mới nhất.
  - Gắn `tripId` khi chuyến `Boarding/EnRoute/Suspended` (BR-INC-05).
  - Ghi lịch sử thưa (mặc định 15 giây/tàu), cache vị trí mới nhất ở Redis (dự phòng bộ nhớ trong).
- REST: `GET /api/v1/tracking/boats`, `GET .../boats/{id}/history`, `POST .../boats/{id}/positions`.
- Giả lập GPS (BR-TRK-01) dọc các `TripStopCall`, mặc định tắt.
- Migration `AddBoatPositionEventIndex`: index `(BoatId, ObservedAt)`.

### G4 — Weather Guard (#8)
- Job Hangfire `weather-guard` (mặc định mỗi 15 phút, lưu job trong SQL Server).
- Đánh giá: gió ≥ 10.6 m/s (≈38 km/h) → `Medium`; gió giật ≥ 17.2 m/s hoặc dông → `High`.
- **Một** Incident thời tiết phạm vi Route, liên kết các chuyến bị ảnh hưởng với `OperationDecision = null`. Chạy lại không tạo trùng.
- **Không đổi `Trip.Status`** (BR-INC-03).
- Phát `weatherAlert` khi tạo mới hoặc tăng mức.
- Provider OpenWeatherMap (timeout, retry chỉ cho lỗi tạm thời) và Fake (demo).
- Dashboard `/hangfire` có Basic Auth.
- API Admin: `GET /api/v1/incidents`, `GET /api/v1/incidents/{id}`, `POST /api/v1/incidents/weather-check`.

### G5 — Seat sync (#9)
- Mỗi kết nối theo dõi tối đa 4 chuyến; `tripId` rỗng bị từ chối.
- **Việc phát event giữ ghế phụ thuộc CheckoutSession của TV2** (xem mục 4).

### Sửa rate limit (#10)
- API sau nginx thấy mọi request đến từ IP nginx → hạn mức đặt vé 10 lần/phút **dùng chung cho mọi người dùng**.
- Sửa: `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.
- Đã đo: trước sửa, client thứ 11 (IP khác) bị 429 oan; sau sửa không còn; giả mạo `X-Forwarded-For` vẫn bị chặn.

## 3. Kiểm thử trên máy (sau khi các PR được merge)

```bash
cp .env.example .env            # điền MSSQL_SA_PASSWORD, REDIS_PASSWORD, JWT_SECRET
docker compose up -d --build    # Web http://localhost:8080
```

Để có Swagger: đặt `ASPNETCORE_ENVIRONMENT=Development` trong `.env`, mở http://localhost:5000/swagger.

Tài khoản mẫu được seed trong `ApplicationDbContextInitializer.cs`.

| Muốn thử | Cấu hình `.env` / thao tác |
|---|---|
| Tàu chạy trên radar | `TRACKING_SIMULATOR_ENABLED=true`, `TRACKING_SIMULATOR_INCLUDE_SCHEDULED=true` |
| Captain gửi GPS thật | Admin gán Captain cho tàu: `PUT /api/v1/boats/{id}` với `captainUserId` |
| Cảnh báo thời tiết | `WEATHER_PROVIDER=Fake`, `WEATHER_FAKE_WIND_MS=15` rồi `POST /api/v1/incidents/weather-check`. Chỉ xét chuyến khởi hành trong `WEATHER_LOOKAHEAD_HOURS` (mặc định 3 giờ) |
| Dashboard job | Đặt `HANGFIRE_DASHBOARD_USER/PASSWORD`, mở http://localhost:5000/hangfire |

Hợp đồng hub/event và code mẫu client: `docs/REALTIME_CONTRACT.md`.

## 4. Còn lại / cần người khác

| Việc | Ai |
|---|---|
| Gọi `ISeatEventPublisher.SeatHeld/Released/SoldAsync` sau khi giữ/nhả/bán ghế commit thành công (CheckoutSession) | TV2 |
| Xoá `KNOWN_FAILING_TESTS_FILTER` trong CI khi 4 test CreateBooking được sửa | TV2 |
| Endpoint Admin đổi `Trip.Status` (Start/Suspend/Cancel/Terminate) + ghi `IncidentTrip.OperationDecision` và `TripOperationEvent` | Cần nhóm chốt |
| Radar map (Leaflet), Captain Portal gửi GPS, dashboard sự cố dùng hợp đồng realtime | Người làm Admin/Captain UI |
| API key OpenWeatherMap thật (`WEATHER_PROVIDER=OpenWeatherMap`, `OPENWEATHERMAP_API_KEY`) | Kiệt |
| Deploy lên AWS | Kiệt, sau khi cả nhóm xong và test local đạt |

### Ghi chú cho lần deploy AWS
- Có bản nháp deploy đã chạy thử trên máy (Caddy HTTPS, script deploy/backup/restore có chốt an toàn, hướng dẫn) ở **commit local `0a9d136`, nhánh `feature/kiet-deploy`, chưa push**. Nhánh đó cũng xoá `waterbus-be/docker-compose.yml` cũ (trùng lặp với compose ở gốc).
- Trên AWS có thể thay Caddy bằng ALB + ACM. Khi đó cần giữ `ASPNETCORE_FORWARDEDHEADERS_ENABLED` và cho phép mạng của ALB làm proxy tin cậy.
- SQL Server cần ≥ 4 GB RAM. Nếu dùng RDS, connection string đổi qua biến môi trường, không cần sửa code.
- Geolocation của Captain Portal chỉ chạy trên HTTPS.
