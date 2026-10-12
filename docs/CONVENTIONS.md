# Smart Waterbus — Quy ước & Quy tắc dự án

> Áp dụng cho cả 6 thành viên. Nguồn chuẩn nghiệp vụ: `Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md`.
> Khi quy ước này mâu thuẫn với `SETUP.md` hoặc `TASK_BREAKDOWN_ROADMAP.md` (viết trước BR v2.0), **file này và BR v2.0 thắng**.
> Phần nào ghi là chưa được cả nhóm xác nhận — chốt trong buổi họp rồi bỏ nhãn.

---

## 1. Cấu trúc repo

```
waterbus-system/
├── docs/            Tài liệu (BR, ERD, quy ước). Không đặt code ở đây.
├── waterbus-be/     .NET 8 Web API (Clean Architecture)
├── waterbus-fe/     Web React + Vite + TypeScript (Customer, Admin, Captain)
├── waterbus-mobile/ App Staff Scanner (chỉ phục vụ soát vé)
└── SETUP.md
```

Theo BR-SCOPE-02: Passenger, Captain, Admin dùng **Web**. Mobile **chỉ** dành cho Staff Scanner.
Không có role Dispatcher (BR-ROLE-01). Năm actor: Passenger, Guest, Admin, Staff, Captain.

## 2. Git

### Nhánh
- `main`: luôn build được. **Không push trực tiếp.**
- Nhánh làm việc: `feature/<tên>-<việc>` (vd `feature/kiet-seat-sync-hub`), sửa lỗi dùng `fix/<tên>-<việc>`, tài liệu dùng `docs/<việc>`.
- Mỗi nhánh làm một việc nhỏ, sống ngắn. Rebase hoặc merge `main` vào nhánh trước khi mở PR.

### Commit
Dạng Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`.
Ví dụ: `feat(realtime): add SeatSyncHub with per-trip groups`.

### Pull Request
- Một PR = một mục đích. Mô tả: làm gì, vì sao, cách kiểm tra.
- Điều kiện merge: `dotnet build` và `dotnet test` (BE) hoặc `npm run build` (FE) **không lỗi**, ít nhất 1 người review.
- Không commit: secrets, `bin/`, `obj/`, `node_modules/`, file `.env`.
- Hiện có **4 test CreateBooking đang fail** (xem `docs/DOMAIN_BR_ALIGNMENT.md`). Không được làm thêm test fail ngoài 4 test đó.

## 3. Backend (.NET 8)

### 3.1 Kiến trúc & chiều phụ thuộc
`WebApi → Application → Domain` và `Infrastructure → Application → Domain`.
- **Domain**: entity, enum, rule thuần. Không tham chiếu EF, MediatR, hay thư viện hạ tầng.
- **Application**: CQRS (MediatR), validator, interface (`IApplicationDbContext`, `IDistributedLockService`, `ICurrentUserService`...).
- **Infrastructure**: EF Core, Redis, Identity/JWT, VNPay, background job, SignalR publisher.
- **WebApi**: controller, middleware, DI, cấu hình Hub.

### 3.2 Feature slice
Mỗi feature một file, đặt tại
`Application/Features/{Feature}/{Commands|Queries}/{Name}/{Name}{Command|Query}.cs`
gồm: DTO, record Command/Query, `AbstractValidator<T>` (nếu có input), Handler.
Mẫu tham khảo: `CreateBookingCommand.cs`, `SearchTripsQuery.cs`.

### 3.3 Controller
- Kế thừa `BaseApiController` (route `api/v1/[controller]`, có sẵn `Mediator`).
- Chỉ gọi `Mediator.Send(...)` và trả `Ok`/`CreatedAtAction`. **Không đặt logic nghiệp vụ trong controller.**
- Gắn `[Authorize(Roles = "...")]` rõ ràng cho mọi endpoint không công khai. Endpoint Guest dùng cơ chế `AccessGrant` (xem `GuestAccessMiddleware`).

### 3.4 Lỗi
Ném exception nghiệp vụ, **không** tự `return BadRequest/Conflict` trong handler. `GlobalExceptionHandlingMiddleware` trả `ProblemDetails` (RFC 7807):

| Exception | HTTP |
|---|---|
| `ValidationException` | 400 |
| `NotFoundException` | 404 |
| `ConcurrencyException` (và `SeatAlreadyBookedException`) | 409 |
| khác | 500 |

Cần mã mới (vd 403, 422) thì thêm `case` vào middleware, không xử lý riêng lẻ.

### 3.5 Dữ liệu & EF Core
- Mọi entity kế thừa `BaseEntity` (Id, CreatedAt/UpdatedAt, `IsDeleted`, `CreatedBy`/`LastModifiedBy`, `RowVersion`).
- Mọi entity mới phải có `IEntityTypeConfiguration<T>` trong `EntityConfigurations.cs`: khoá, unique cho mã nghiệp vụ, độ dài chuỗi, `RowVersion.IsRowVersion()`, `DeleteBehavior` tường minh (mặc định `Restrict`).
- Query chỉ đọc luôn dùng `AsNoTracking()`.
- Xoá dữ liệu danh mục là **soft delete** (`IsDeleted = true`) và query phải lọc `!IsDeleted`. Không `Remove()` entity nghiệp vụ.
- Ràng buộc bất biến đặt ở DB bằng check constraint / composite FK khi có thể (xem cách làm ở `Booking`, `FinancialTransaction`).
- Thời gian dùng `DateTimeOffset`, lưu **UTC**. Chỉ đổi múi giờ (Asia/Ho_Chi_Minh) ở tầng hiển thị.
- Tiền dùng `decimal(12,2)`, không dùng `double`. Toạ độ dùng `decimal(10,7)`.
- Tên enum/trạng thái: giữ giá trị số hiện có, **không đổi số của enum đã tồn tại**.

### 3.6 Migration
- Chỉ **một người tại một thời điểm** tạo migration. Báo trong nhóm trước khi tạo, `git pull` trước.
- Tên migration mô tả nội dung (`AddBoatPositionIndex`), không dùng `Update1`.
- Lệnh:
  ```bash
  dotnet ef migrations add <Name> --project src/WaterbusSystem.Infrastructure --startup-project src/WaterbusSystem.WebApi
  ```
- Không sửa migration đã merge. Cần chỉnh thì tạo migration mới.
- Migration có thể phá dữ liệu phải có bước kiểm tra trước (như `CompleteBookingJourneyDomain`), không đoán dữ liệu.
- Không dùng entity/field legacy (`PaymentTransaction`, `Booking.TotalAmount`, `TicketStatus.Pending`...) trong code mới.

### 3.7 Đặt ghế & tiền (quy tắc bắt buộc)
- Luồng: `PurchaseOrder → Booking → SeatReservation → Ticket`. **Ticket chỉ được phát sau thanh toán thành công** (`Ticket.Issue()`).
- Ghế giữ theo **đoạn** dùng `VisitOrder` của `TripStopCall`, không dùng `Station.OrderIndex`.
- Chống đặt trùng 2 lớp: Redis lock (TTL 10 phút) + RowVersion/transaction. Chặn chồng lấn đoạn do service làm trong transaction (DB không có unique Trip+Seat).
- Mọi giao dịch tiền có `IdempotencyKey`. Webhook/IPN phải idempotent và kiểm chữ ký bằng `CryptographicOperations.FixedTimeEquals`.
- Luôn release lock ở nhánh lỗi; chỉ giữ lock ở nhánh thành công đến khi thanh toán xác nhận hoặc hết TTL.

### 3.8 Bảo mật & cấu hình
- **Không** commit secret. Local dùng `dotnet user-secrets`; deploy dùng biến môi trường (`ConnectionStrings__DefaultConnection`, `JwtSettings__Secret`, `VnPay__HashSecret`...).
- `appsettings.json` chỉ chứa giá trị không nhạy cảm. Thêm key mới thì cập nhật cả `appsettings.example.json`.
- Swagger chỉ bật ở Development.
- Không log dữ liệu cá nhân (email, SĐT), token, hay payload thanh toán đầy đủ.
- Token Guest chỉ lưu **hash** (`AccessGrant.TokenHash`), không bao giờ lưu token gốc.

### 3.9 Real-time (SignalR)
- Hub đặt trong `WebApi/Hubs/`, đường dẫn `/hubs/seat-sync` và `/hubs/boat-tracking`.
- Application chỉ phụ thuộc interface (vd `ISeatEventPublisher`, `IBoatTrackingPublisher`). Implementation dùng `IHubContext` đặt ở Infrastructure/WebApi. **Handler không tham chiếu Hub trực tiếp.**
- Group: `trip:{tripId}` cho ghế, `boat:{boatId}` và `fleet` cho vị trí tàu.
- Tên event camelCase, payload là DTO có `version`:
  - `seatHeld` / `seatReleased` / `seatSold`: `{ tripId, seatId, boardingVisitOrder, disembarkingVisitOrder, status, expiresAt? }`
  - `boatPosition`: `{ boatId, tripId?, lat, lng, speedKnots?, observedAt }`
  - `weatherAlert`: `{ incidentId, severity, routeId?, message, occurredAt }`
- Hub client gọi lên chỉ để join/leave group. Việc giữ/nhả ghế vẫn đi qua REST API; hub **không** là nơi quyết định nghiệp vụ.
- Xác thực qua JWT trong query `access_token`. Gửi GPS: chỉ role Captain. Nhận bản đồ radar: Admin. Hub ghế cho khách: cho phép Guest chỉ đọc.
- Hub phát tin; mất kết nối không được làm sai dữ liệu. Client phải gọi lại REST để đồng bộ khi kết nối lại.

### 3.10 Background job
- Job định kỳ dùng Hangfire (lưu trong SQL Server). Dashboard `/hangfire` chỉ cho role Admin.
- Job phải **idempotent** (chạy lặp không tạo bản ghi trùng; ví dụ không tạo Incident thời tiết trùng cho cùng tuyến và cùng khoảng thời gian).
- Weather Guard chỉ **tạo Incident** (`Source = "WeatherService"`, `IncidentType = "Weather"`). Nó **không được đổi `Trip.Status`**; quyết định Suspend/Cancel là của Admin qua `IncidentTrip` (BR).
- Mỗi lần `Trip.Status` đổi phải ghi một `TripOperationEvent`.
- Ngưỡng cảnh báo (tốc độ gió, chu kỳ quét, API key) đặt trong cấu hình, không hard-code.
- Gọi dịch vụ ngoài phải có timeout và retry có giới hạn, lỗi chỉ ghi log, không làm sập host.

### 3.11 Đặt tên
- C#: `PascalCase` cho type/method/property, `_camelCase` cho field private, interface bắt đầu bằng `I`.
- Command/Query: `CreateStationCommand`, `GetTripDetailQuery`. Handler: `...Handler`. Validator: `...Validator`.
- Tên bảng/cột do EF sinh theo tên entity. Không đổi tên thủ công trừ khi có lý do.
- Comment/XML-doc viết tiếng Việt như code hiện tại; tên định danh viết tiếng Anh.

### 3.12 Test
- Dự án: `tests/WaterbusSystem.UnitTests`, tách thư mục theo module (`Bookings/`, `Payments/`, `Domain/`...).
- Mỗi handler/rule nghiệp vụ mới cần ít nhất test cho: đường chính, một trường hợp lỗi, một cạnh biên.
- Test chạm SQL Server thật đặt biến `WATERBUS_TEST_SQLSERVER`, tự tạo và xoá DB `WaterbusDomainTests_*`. Không trỏ vào DB dev.
- Lệnh:
  ```bash
  cd waterbus-be
  dotnet build WaterbusSystem.sln
  dotnet test WaterbusSystem.sln
  ```

## 4. Frontend (React + TypeScript)

- Giữ cấu trúc boilerplate: `pages/` (màn hình), `features/` (slice + api + hook theo nghiệp vụ), `components/` (dùng chung), `layouts/`, `routes/`, `store/`, `hooks/`, `constant/`.
- Không gọi `fetch` rải rác: mọi API đi qua `constant/apiEndpoints.ts` và service trong `features/<x>/services`.
- Base URL API, URL Hub, khoá bản đồ: đặt trong `.env` với tiền tố `VITE_`; có `.env.example`.
- Gửi header `Accept-Language` theo ngôn ngữ đang chọn (backend có `RequestLocalizationMiddleware`).
- Route và quyền: `PrivateRoute` kiểm role; Guest dùng link bảo mật (`AccessGrant`), không bắt đăng nhập.
- Realtime: một service bọc kết nối SignalR (tự reconnect, join lại group, gọi REST đồng bộ lại sau reconnect).
- Tiền hiển thị theo `vi-VN`, thời gian lấy UTC từ API và format theo múi giờ Việt Nam.
- Component: `PascalCase.tsx`; hook: `useXxx`; không dùng `any` nếu tránh được.
- `npm run build` phải qua (gồm `tsc`) trước khi mở PR.

## 5. Mobile (Staff Scanner)

- Chỉ soát vé. Preload danh sách vé hợp lệ, thông tin chuyến và Public Key trước giờ mở cổng.
- Xác thực QR **offline** bằng chữ ký số (Public Key). Không gọi API cho mỗi lần quét.
- Mỗi sự kiện check-in sinh `clientEventId` duy nhất; đẩy lên server theo lô, server khử trùng bằng `CheckInEvent.ClientEventId`.
- Chỉ check-in trong cửa sổ `CheckInOpenAt`–`CheckInCloseAt` của đúng `TripStopCall` được phân công (`ScannerAssignment`).

## 6. API

- Tiền tố `/api/v1/...`, JSON `camelCase`, id là GUID.
- Lỗi luôn là `ProblemDetails` (có `traceId`); danh sách lỗi validate nằm trong `errors`.
- Danh sách trả về cần phân trang khi có thể lớn (`page`, `pageSize`).
- Đổi hợp đồng API (field, event) phải báo trong nhóm và cập nhật tài liệu / Swagger **trước khi merge**, vì FE, mobile và AI cùng phụ thuộc.

## 7. DevOps & môi trường

- Local: `docker compose up -d` (SQL Server 2022 + Redis 7). Sao `.env.example` thành `.env` và đặt mật khẩu mạnh.
- Dịch vụ cần có khi triển khai: sqlserver, redis, api, frontend (nginx), reverse proxy HTTPS.
- Mọi cấu hình đi qua biến môi trường; không có giá trị cố định theo máy.
- API tự chạy migration và seed demo khi khởi động. Môi trường production thật phải xem xét lại điều này.
- Mỗi service có healthcheck. Volume SQL Server phải được backup trước khi nâng migration.
- CI (khi có): build + test BE, build FE, mỗi PR.

## 8. Định nghĩa "xong" (Definition of Done)

- [ ] Code build, test liên quan pass, không thêm test fail mới.
- [ ] Tuân thủ BR v2.0 và quy tắc ở mục 3.7 nếu chạm tiền hoặc ghế.
- [ ] Có validate đầu vào và xử lý lỗi qua exception chuẩn.
- [ ] Có phân quyền rõ ràng cho endpoint.
- [ ] Không có secret trong diff.
- [ ] Đã cập nhật tài liệu / Swagger nếu đổi API hoặc event.
- [ ] Đã tự thử chạy thật (Swagger hoặc UI), không chỉ dựa vào unit test.
