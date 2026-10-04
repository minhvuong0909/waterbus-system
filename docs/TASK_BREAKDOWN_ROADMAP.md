# PHÂN RÃ CÔNG VIỆC & ĐIỀU PHỐI NHIỀU AI MODEL — SMART WATERBUS
> Tài liệu này chia toàn bộ roadmap 4 giai đoạn (Web + Mobile + Backend) thành các **module độc lập**, mỗi module đủ nhỏ để giao cho **một AI model/session riêng** xử lý song song, kèm phụ thuộc rõ ràng để tránh đụng độ. Dựa trên đặc tả gốc do Team Lead cung cấp + audit thực tế codebase `waterbus-be` tại thời điểm viết tài liệu (2026-09-21).

---

## 0. QUYẾT ĐỊNH CÒN MỞ (chốt trước khi giao việc)

| # | Vấn đề | Vì sao cần chốt trước | Đề xuất mặc định nếu không chốt |
|---|---|---|---|
| 1 | **Stack Web Frontend** chưa xác định (React/Vite, Next.js, Vue...) | Quyết định convention code cho toàn bộ track Web | Vite + React + TypeScript (suy ra từ `ReturnUrl: http://localhost:5173` đã cấu hình sẵn trong `appsettings.json`) |
| 2 | **Stack Mobile** chưa chốt (Flutter / React Native / .NET MAUI) | 3 lựa chọn dùng ngôn ngữ & convention hoàn toàn khác nhau, không thể vừa làm vừa đổi | Cần Team Lead chọn 1 trước khi giao bất kỳ task `MOB-*` nào |
| 3 | Cổng thanh toán **MoMo** (nêu ở mục 2.1) có bắt buộc Phase 1 không? | Roadmap bảng mục 5 chỉ ghi "Tích hợp VNPAY IPN" cho Phase 1 | Coi MoMo là backlog Phase 2+, không chặn Phase 1 |
| 4 | **AI Assistant Widget (Semantic Kernel)** thuộc phase nào? | Không xuất hiện trong bảng roadmap mục 5 dù được mô tả kỹ ở mục 2.1 | Xếp vào Phase 4 (optional), vì phụ thuộc Trip Search + Booking đã ổn định |
| 5 | Quản lý secrets (JWT Secret, VNPay HashSecret, SA password) đang nằm thẳng trong `appsettings.json` | Đã flag ở review trước, chưa xử lý | Cần quyết định: User Secrets (dev) + Key Vault/Env Var (prod) — giao cho `P1-BE-09` |

---

## 1. TÌNH TRẠNG HIỆN TẠI (audit thực tế `waterbus-be`, không phải suy đoán)

✅ **Đã có & chạy được** (build 0 lỗi, test 8/8 pass):
- Clean Architecture scaffold đầy đủ 5 project, đúng chiều phụ thuộc.
- Domain entities: Station, Route, Boat, Seat, Schedule, Trip, Booking, Ticket, PaymentTransaction + toàn bộ Enums.
- Identity/JWT (ASP.NET Core Identity, 6 roles seed sẵn: Admin, Dispatcher, Accountant, Captain, Staff, Passenger).
- CORS whitelist theo config (vừa fix), Distributed Lock qua StackExchange.Redis thuần (vừa fix, bỏ RedLock.net cũ), VNPay HMAC-SHA512 + FixedTimeEquals (vừa fix lỗi build IQueryCollection).
- `CreateBookingCommand`: đúng 2 lớp phòng thủ (Redis lock + EF Core RowVersion) như thiết kế.
- `SearchTripsQuery`, `GetStationsQuery` (read-only).
- Seed data: 5 bến, 1 tuyến, 1 tàu 60 ghế (3 khoang), 3 tài khoản mẫu.

⚠️ **Còn thiếu / dở dang** — đây chính là phần lớn khối lượng Phase 1 Backend còn lại:
- **Không có Trip nào được seed hoặc sinh ra** → `SearchTripsQuery` hiện trả về rỗng, luồng đặt vé end-to-end **chưa thể test thật** cho tới khi có Fixed Schedule Engine.
- Routes, Boats: chỉ có entity + seed cứng, **0% CRUD API** (`RoutesController`, `BoatsController` chưa tồn tại dù có trong bản thiết kế `SETUP.md` gốc).
- Stations: chỉ có GET list, thiếu Create/Update/Delete cho Admin.
- Schedule (Lịch chạy cố định): chỉ có entity, chưa có CRUD lẫn cơ chế sinh Trip hàng loạt.
- Auth: `LoginCommand` (CQRS record) tồn tại nhưng **không được dùng** — `AuthController.Login` viết logic trực tiếp bằng `UserManager`/`SignInManager`, bỏ qua MediatR. Thiếu Register, thiếu Admin user management, `ICurrentUserService` mới chỉ có interface chưa có implementation.
- `GetBookingByCode` Query (có trong cây thư mục thiết kế gốc) chưa được tạo.
- Chưa có cơ chế tự huỷ Booking `Pending` quá hạn chưa thanh toán (Redis lock tự hết hạn nhưng bản ghi DB thì không).
- `Program.cs` dòng 86 vẫn còn `if (app.Environment.IsDevelopment() || true)` — Swagger bật ở mọi môi trường kể cả Production (đã flag, chưa fix).
- Secrets vẫn hardcode trong `appsettings.json` (đã flag, chưa fix).
- Web (`waterbus-web`) và Mobile (`waterbus-mobile`): **chưa khởi tạo**, chỉ có `waterbus-be/`.

---

## 2. NGUYÊN TẮC CHIA VIỆC CHO NHIỀU AI MODEL

1. **Đơn vị giao việc = 1 module** (bảng bên dưới), không giao theo cả nền tảng để tránh 1 model ôm việc quá lâu và đụng độ file với model khác trên cùng track.
2. **Mỗi module chạy trên 1 branch/1 workspace riêng** (`feature/p1-be-02-routes-crud` chẳng hạn). Không 2 model nào sửa cùng file trong cùng thời điểm — tra cột **Phụ thuộc** trước khi giao song song.
3. **Thứ tự bắt buộc**: trong cùng 1 Phase, module không có phụ thuộc → giao trước/song song; module có phụ thuộc → chỉ giao sau khi module gốc đã merge.
4. Mỗi module khi giao cho model cần kèm: đường dẫn `TASK_BREAKDOWN_ROADMAP.md` này (để model tự đọc phần của mình) + `SETUP.md` (convention code Backend) + Definition of Done cụ thể trong bảng.
5. Model nhận việc **luôn chạy `dotnet build` + `dotnet test`** (Backend) hoặc `npm run build`/`flutter analyze` (Web/Mobile, tuỳ stack chốt ở mục 0) trước khi báo hoàn thành.

---

## 3. PHASE 1 — NỀN TẢNG & ĐẶT VÉ

### Backend

| ID | Module | Phụ thuộc | Deliverables chính | DoD |
|---|---|---|---|---|
| P1-BE-01 | CRUD Bến (Stations) | — | `CreateStation`, `UpdateStation`, `DeleteStation` Commands + mở rộng `StationsController` (POST/PUT/DELETE), FluentValidation | Admin tạo/sửa/xoá bến qua Swagger, có validate trùng `Code` |
| P1-BE-02 | CRUD Tuyến (Routes) | P1-BE-01 | `RoutesController` + Commands/Queries đầy đủ, validate `DepartureStationId != ArrivalStationId` | CRUD tuyến hoạt động, liên kết đúng StationId có thật |
| P1-BE-03 | CRUD Tàu & Ghế (Boats & Seats) | — | `BoatsController` + Command tạo tàu kèm sinh layout ghế tự động (giống logic seed hiện có, tổng quát hoá thành service) | Tạo tàu mới với N ghế theo 3 khoang, GET trả về sơ đồ ghế |
| P1-BE-04 | Lịch chạy cố định (Fixed Schedule Engine) | P1-BE-02, P1-BE-03 | CRUD `Schedule` + job/endpoint sinh `Trip` hàng loạt theo `DaysOfWeek` trong 1 khoảng ngày, set `TripType` (Commuter/Sightseeing) | Gọi 1 API sinh ra N Trip có thể `SearchTrips` tìm thấy |
| P1-BE-05 | Hoàn thiện Trip & Booking Query | P1-BE-04 | `GetTripDetailQuery` (sơ đồ ghế theo trip), `GetBookingByCodeQuery` | Có thể tra cứu 1 Trip cụ thể ra đủ ghế trống/đã bán |
| P1-BE-06 | Auth hoàn thiện | — | Refactor `Login` sang đúng CQRS (`LoginCommandHandler`), thêm `RegisterCommand`, implement `ICurrentUserService` (đọc JWT Claims từ `HttpContext`), Admin user management endpoints | Đăng ký/đăng nhập qua MediatR nhất quán với Booking; `ICurrentUserService` có impl thật |
| P1-BE-07 | Booking cleanup job | P1-BE-05 | Cơ chế huỷ `Booking.Pending` quá hạn (đối chiếu Redis lock hết hạn) — có thể tạm dùng `IHostedService` đơn giản, Hangfire để dành Phase 4 | Booking không thanh toán sau 10 phút tự chuyển `Cancelled`, ghế về `Available` |
| P1-BE-08 | Hardening còn lại | — | Fix `Program.cs` dòng 86 (bỏ `\|\| true`), chuyển secrets sang User Secrets (dev) + đọc từ biến môi trường (prod-ready) | Swagger tắt ở Production; không còn secret plaintext trong file commit |

### Web (khởi tạo mới `waterbus-web/`)

| ID | Module | Phụ thuộc | Deliverables chính | DoD |
|---|---|---|---|---|
| P1-WEB-00 | Khởi tạo project | Mục 0.1 đã chốt stack | Scaffold project, routing, layout shell, API client wrapper trỏ `waterbus-be` | `npm run dev` chạy được, gọi thử `GET /api/v1/stations` thành công |
| P1-WEB-01 | Trang chủ & Trip Finder | P1-WEB-00, P1-BE-04/05 | Filter chuyến (điểm đi/đến/ngày/loại), Trip Card phân biệt Commuter/Sightseeing | Tìm được chuyến thật từ dữ liệu do P1-BE-04 sinh ra |
| P1-WEB-02 | i18n (vi/en/ko/zh) | P1-WEB-00 | JSON resource 4 ngôn ngữ, switcher, gửi `Accept-Language` header | Đổi ngôn ngữ tức thời không reload, backend nhận đúng header |
| P1-WEB-03 | Sơ đồ ghế tĩnh | P1-BE-03, P1-BE-05 | Component render layout ghế theo khoang, mã màu trạng thái cơ bản (chưa cần real-time — thuộc Phase 2) | Hiển thị đúng layout ghế thật của 1 Trip |
| P1-WEB-04 *(khuyến nghị)* | Admin CRUD UI cơ bản | P1-BE-01/02/03/04 | Form/bảng CRUD Bến-Tuyến-Tàu-Lịch dạng đơn giản | Admin nhập dữ liệu thật qua UI thay vì chỉ dựa seed cứng |

### Mobile (khởi tạo mới `waterbus-mobile/`)

| ID | Module | Phụ thuộc | Deliverables chính | DoD |
|---|---|---|---|---|
| P1-MOB-00 | Chốt stack + khởi tạo project | Mục 0.2 | Scaffold theo framework đã chọn, kết nối thử API Login | Build & chạy được trên emulator |
| P1-MOB-01 | Khung app 3 chế độ | P1-MOB-00, P1-BE-06 | Navigation/Shell, chuyển chế độ Captain/Staff/Passenger theo Role trả về từ Login | Login xong vào đúng màn hình theo Role |
| P1-MOB-02 | Tích hợp Camera quét QR (cơ bản) | P1-MOB-00 | Quét & hiển thị raw text (chưa giải mã TOTP — thuộc Phase 2) | Quét QR bất kỳ ra được nội dung text |
| P1-MOB-03 | Thiết lập SQLite | P1-MOB-00 | Schema cục bộ cho user/vé (chưa cần sync logic — thuộc Phase 4) | Ghi/đọc được 1 bản ghi mẫu vào SQLite |

---

## 4. PHASE 2 — REAL-TIME & GIỮ GHẾ

### Backend
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P2-BE-01 | SignalR `BookingHub` | P1-BE-07, `RedisDistributedLockService` hiện có | Broadcast `SeatLocked`/`SeatUnlocked` khi acquire/release lock | Client SignalR nhận event đúng payload spec mục 4 |
| P2-BE-02 | Integration test Optimistic Concurrency | P1-BE-05 | Test thật với DB (không mock) cho `DbUpdateConcurrencyException` | 2 request đồng thời → đúng 1 thành công |
| P2-BE-03 | Dynamic TOTP QR Seed | P1-BE-05 | Sinh `QrSeed` (RFC 6238) khi tạo Ticket, endpoint lấy mã hiện tại | Mã QR đổi mỗi 30s theo chuẩn TOTP |
| P2-BE-04 | API Check-in vé | P2-BE-03 | `POST /api/v1/tickets/checkin` theo payload spec mục 4 | Vé hợp lệ → `CheckedIn`; vé sai/hết hạn → lỗi rõ ràng |

### Web
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P2-WEB-01 | SignalR client seat map | P2-BE-01, P1-WEB-03 | Nghe `SeatLocked/Unlocked`, cập nhật UI real-time | 2 tab trình duyệt thấy ghế đổi màu đồng bộ |
| P2-WEB-02 | Countdown 600s UI | P2-WEB-01 | Đồng hồ đếm ngược, tự nhả ghế khi hết giờ | UI khớp với TTL Redis thật |
| P2-WEB-03 | Xuất vé + QR động | P2-BE-03 | Trang vé hiển thị QR tự xoay vòng | QR hiển thị đổi theo đúng chu kỳ backend |

### Mobile
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P2-MOB-01 | Giải mã TOTP QR | P2-BE-03 | Camera + thuật toán TOTP đối chiếu | Quét đúng cho kết quả valid/invalid chính xác |
| P2-MOB-02 | Ví vé Offline cơ bản | P1-MOB-03, P2-BE-03 | Lưu vé + QrSeed vào SQLite, hiển thị QR không cần mạng | Tắt mạng vẫn xem được mã vé hợp lệ |
| P2-MOB-03 | Phản hồi rung/âm thanh soát vé | P2-BE-04 | Haptic + sound khi checkin thành công/thất bại | Đúng theo mô tả UI mục 3.2 |

---

## 5. PHASE 3 — SMART AUDIO & TRACKING

### Backend
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P3-BE-01 | SignalR `BoatTrackingHub` | Phase 2 xong | Nhận GPS kép (AIS + App), lưu + broadcast | Vị trí cập nhật tới client < 3s |
| P3-BE-02 | Geofence + ETA (NetTopologySuite) | P3-BE-01 | Tính khoảng cách tới POI/bến, dự báo ETA | Sai số khoảng cách chấp nhận được so với thực tế |
| P3-BE-03 | CRUD POI + Azure TTS | — | Quản lý điểm thuyết minh, sinh audio tự động | Tạo 1 POI → có file audio khả dụng |

### Web
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P3-WEB-01 | Radar Fleet (Leaflet/Mapbox) | P3-BE-01 | Bản đồ realtime cho Dispatcher | Icon tàu di chuyển mượt theo dữ liệu thật |
| P3-WEB-02 | Quản lý POI & kịch bản | P3-BE-03 | UI CRUD POI | Admin cấu hình được POI mới không cần deploy lại |

### Mobile
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P3-MOB-01 | GPS nền tần suất cao (Captain) | P3-BE-01 | Background location gửi mỗi 3s | Hoạt động ổn định khi app ở background |
| P3-MOB-02 | Audio Controller thông minh | P3-BE-03 | Auto bật/tắt theo TripType, nút Pause/Replay/Cắt ưu tiên | Đúng hành vi mô tả mục 3.1 |
| P3-MOB-03 | Thuyết minh cá nhân (Passenger) | P3-BE-01/02, P3-BE-03 | Phát audio theo vị trí + ngôn ngữ đã chọn | Đổi ngôn ngữ → đổi audio đúng |
| P3-MOB-04 | Arrival Countdown HUD (Captain) | P3-BE-02 | Cảnh báo 400-500m trước khi cập bến | Cảnh báo đúng ngưỡng khoảng cách |

---

## 6. PHASE 4 — AN TOÀN, TỰ PHỤC VỤ & KIỂM THỬ

### Backend
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P4-BE-01 | Weather Gatekeeper Worker | — | Hosted Service quét API thời tiết mỗi 15p, ngưỡng gió >38km/h | Cảnh báo Mức 1 lên Dashboard đúng ngưỡng |
| P4-BE-02 | Emergency Button API | P4-BE-01, P2-BE-01 | Ngưng tuyến: khoá bán vé, giải phóng toàn bộ Redis lock của Trip, gửi SMS/Email | 1 lệnh → toàn bộ ghế Trip về trạng thái trống, khách nhận thông báo |
| P4-BE-03 | Incident Self-Service | P4-BE-02 | Magic Link, 3 API: đổi chuyến/hoàn tiền/voucher 110% (Hangfire) | Khách tự xử lý được không cần gọi tổng đài |
| P4-BE-04 | Hangfire + Idempotent Batch Sync | P2-BE-04 | `POST /api/v1/tickets/sync-offline` với Idempotency Key | Gửi trùng batch không tạo dữ liệu trùng |
| P4-BE-05 | Analytics API | Toàn bộ Booking/Payment | Doanh thu theo thời gian, occupancy rate, Commuter vs Sightseeing | Số liệu khớp dữ liệu thật trong DB |
| P4-BE-06 | Mã hoá dữ liệu liên hệ (AES-256) | — | Encrypt/decrypt `CustomerPhone`/`CustomerEmail` theo NĐ13/2023 | Dữ liệu trong DB không đọc được plaintext |
| P4-BE-07 | Observability (Serilog + OpenTelemetry + Prometheus) | — | Structured log JSON, tracing xuyên tầng, metrics theo mục 6.1 | Trace 1 request từ Client → RedLock → EF Core xem được đầy đủ |
| P4-BE-08 | Backup/DR scripts | — (hạ tầng, không phải app code) | Script Full/Diff/Log backup SQL Server, cấu hình Always On | Đạt RPO<5p/RTO<30p theo mục 6.3 (môi trường thật, giao riêng cho model chuyên Infra) |

### Web
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P4-WEB-01 | Cổng Tự Phục Vụ khi bão | P4-BE-03 | UI đổi vé/hoàn tiền/voucher | Luồng 1-chạm hoạt động đúng 3 lựa chọn |
| P4-WEB-02 | Dashboard doanh thu | P4-BE-05 | Biểu đồ doanh thu + occupancy | Dữ liệu realtime khớp Analytics API |
| P4-WEB-03 *(optional)* | AI Assistant Widget | Trip Search + Booking ổn định | Chat widget + Function Calling tạo link đặt vé | Trả lời đúng giờ tàu, tạo được link đặt vé |

### Mobile
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P4-MOB-01 | Offline-First Queue Sync hoàn thiện | P4-BE-04, P2-MOB-03 | Hàng đợi batch sync tự động khi có mạng | Mất mạng → soát vé offline → có mạng lại tự đồng bộ không trùng |
| P4-MOB-02 | UI tương phản cao ngoài trời | — | Theme polish cho Staff/Captain mode | Đọc rõ dưới nắng gắt (test bằng độ tương phản) |
| P4-MOB-03 | Pre-cache vé trước chuyến 30p | P4-BE-04 | Tải trước danh sách vé + Public Key về SQLite | Vé pre-cache đúng, đối soát offline chính xác |

### Cross-cutting
| ID | Module | Phụ thuộc | Deliverables | DoD |
|---|---|---|---|---|
| P4-QA-01 | Load Testing | Toàn bộ Backend Phase 1-2 | Script k6/JMeter: 5,000 CCU, 200 hold/s | Đạt ngưỡng tải theo roadmap mục 5, không lỗi 5xx > 1.5% |

---

## 7. GỢI Ý CẤU TRÚC THƯ MỤC SAU KHI KHỞI TẠO WEB/MOBILE

```
WaterbusSystem/
├── SETUP.md                      # Spec gốc Backend (đã có)
├── TASK_BREAKDOWN_ROADMAP.md     # Tài liệu này
├── waterbus-be/                  # Đã có — Backend .NET 8
├── waterbus-web/                 # Mới — tạo ở P1-WEB-00
└── waterbus-mobile/              # Mới — tạo ở P1-MOB-00
```

Mỗi model nhận việc nên được trỏ đúng 1 module ID trong tài liệu này, kèm câu lệnh dạng:
> "Đọc `TASK_BREAKDOWN_ROADMAP.md`, thực hiện module **P1-BE-02**. Chỉ sửa/tạo file liên quan tới module này, không động vào phần của module khác."
