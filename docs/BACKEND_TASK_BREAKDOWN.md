# PHÂN RÃ CHI TIẾT BACKEND — SMART WATERBUS (.NET 8)
> Chi tiết hoá track **Backend** trong `TASK_BREAKDOWN_ROADMAP.md`, dựa trên audit trực tiếp source code hiện có (không suy đoán). Mỗi module ghi rõ: file cần tạo/sửa, contract (Command/Query/DTO), quy tắc nghiệp vụ đặc thù, cạnh biên (edge case), và Definition of Done — đủ để giao thẳng cho 1 AI model thực thi độc lập.

---

## 0. PHÁT HIỆN QUAN TRỌNG TỪ AUDIT CODE (chưa từng được ghi nhận trước đây)

| # | Phát hiện | File liên quan | Mức độ |
|---|---|---|---|
| 1 | **Chưa có EF Core Migration nào được tạo** (thư mục `Migrations/` không tồn tại). `ApplicationDbContextInitializer.InitialiseAsync()` gọi `Database.MigrateAsync()` nhưng không có gì để áp dụng → **schema DB thật chưa bao giờ được tạo ra**. Toàn bộ backend hiện tại **chưa thể chạy thật với SQL Server** dù build/unit-test pass (unit test không đụng DB thật). | `Infrastructure/Persistence/` | 🔴 Blocker — phải làm trước mọi module khác |
| 2 | **Lock leak khi xảy ra `DbUpdateConcurrencyException`** trong `CreateBookingCommandHandler`: catch block đầu tiên `throw new ConcurrencyException(...)` ném exception mới ra khỏi toàn bộ `try/catch`, **không rơi vào** `catch (Exception)` phía sau (2 catch là anh em, không lồng nhau) → Redis lock đã acquire **không được release ngay**, phải chờ TTL 10 phút tự hết hạn. Vé khác không đặt được ghế đó trong 10 phút dù giao dịch đã fail hẳn. | `Application/Features/Bookings/Commands/CreateBooking/CreateBookingCommand.cs` dòng 165-169 | 🟠 Bug thật, ảnh hưởng UX đặt vé |
| 3 | **`SeatStatus` enum (Available/Held/Sold/Blocked) được định nghĩa nhưng không hề được dùng ở đâu.** Trạng thái ghế theo từng chuyến hiện được **suy luận động** từ `Ticket.Status` (Pending/Valid/CheckedIn) join theo `TripId + SeatId`, không lưu field riêng. Bất kỳ API "sơ đồ ghế theo chuyến" nào (P1-BE-05) phải tính lại theo cách `SearchTripsQuery` đang làm (đếm Ticket), **không được** đọc trực tiếp field status trên `Seat`. | `Domain/Enums/SeatStatus.cs`, cách dùng thực tế ở `SearchTripsQuery.cs` dòng 79-84 | 🟡 Cần biết để không code sai |
| 4 | `Schedule` **không có `EntityTypeConfiguration`** (không nằm trong `EntityConfigurations.cs`) — thiếu unique index, thiếu cấu hình FK tới `Route`. RowVersion vẫn hoạt động nhờ `[Timestamp]` trên `BaseEntity` (data annotation không cần Fluent API), nhưng quan hệ `Schedule.RouteId → Route` đang chạy theo convention ngầm, chưa tường minh. | `Infrastructure/Persistence/Configurations/EntityConfigurations.cs` | 🟡 Nợ kỹ thuật nhỏ |
| 5 | `Schedule` **không có `BoatId`** — chỉ có `RouteId`, `DepartureTime`, `DaysOfWeek`, `TripType`. Nghĩa là khi sinh `Trip` hàng loạt từ `Schedule` (Fixed Schedule Engine), **phải có thêm input chọn Boat** (Schedule không tự quyết định tàu nào chạy) — đây là quyết định thiết kế cần chốt ở P1-BE-04, không phải lỗi, nhưng dễ bị code nhầm là "copy BoatId từ Schedule". | `Domain/Entities/Schedule.cs`, `Trip.cs` | 🟡 Cần thiết kế đúng ngay từ đầu |
| 6 | `AuthController.Login` **không đi qua MediatR** dù `LoginCommand` (record `IRequest<AuthResponseDto>`) đã tồn tại — controller gọi thẳng `UserManager`/`SignInManager`. `LoginCommand.cs` hiện là **dead code một phần** (record tồn tại, không có Handler, không được Send). | `WebApi/Controllers/AuthController.cs`, `Application/Features/Auth/Commands/Login/LoginCommand.cs` | 🟡 Vi phạm nhất quán CQRS toàn hệ thống |
| 7 | `TicketConfiguration` không khai báo quan hệ `Ticket.Trip` tường minh (chỉ có `Ticket.Seat`) — EF Core convention tự suy ra vì tên khớp chuẩn, **hoạt động đúng nhưng không tường minh**, nên giữ nguyên convention khi thêm entity mới thay vì tự ý đổi cách đặt tên. | `EntityConfigurations.cs` dòng 119-135 | ⚪ Ghi chú, không cần sửa |

---

## 1. QUY ƯỚC BẮT BUỘC KHI CODE MODULE MỚI (rút từ code hiện có, không phải suy đoán)

- **1 file = 1 feature slice**: DTO + Command/Query record + `AbstractValidator<T>` (nếu có input) + Handler nằm chung 1 file, đúng namespace `WaterbusSystem.Application.Features.{Feature}.{Commands|Queries}.{Name}.{Name}{Command|Query}.cs` — xem `CreateBookingCommand.cs`, `SearchTripsQuery.cs` làm mẫu.
- **Controller mỏng tuyệt đối**: kế thừa `BaseApiController` (đã có `Mediator` property, route prefix `api/v1/[controller]` tự động), chỉ gọi `await Mediator.Send(command)` rồi trả `Ok/CreatedAtAction`, không chứa logic nghiệp vụ (ngoại lệ: `AuthController` hiện đang phá lệ này — không lặp lại kiểu đó cho module mới).
- **Exception nghiệp vụ ném từ Domain/Application**, không tự trả `BadRequest`/`Conflict` thủ công trong Handler — `GlobalExceptionHandlingMiddleware` đã map sẵn: `ValidationException`→400, `ConcurrencyException`(và `SeatAlreadyBookedException` kế thừa nó)→409, `NotFoundException`→404, còn lại→500. Thêm loại lỗi mới phải thêm case vào middleware này.
- **Mọi Entity mới phải có `IEntityTypeConfiguration<T>`** thêm vào `EntityConfigurations.cs`, tối thiểu: `HasKey`, `HasIndex(...).IsUnique()` cho mã nghiệp vụ, `Property(x => x.RowVersion).IsRowVersion()`.
- **AsNoTracking()** bắt buộc cho mọi Query (đã thấy pattern nhất quán ở `GetStationsQuery`, `SearchTripsQuery`).
- **Soft delete**: luôn filter `!x.IsDeleted` trong Query, không dùng `_context.X.Remove()` cho nghiệp vụ CRUD Admin — set `IsDeleted = true`.

---

## 2. PHASE 1 BACKEND — CHI TIẾT TỪNG MODULE

### P1-BE-00 — 🔴 Tạo EF Core Migration đầu tiên (BẮT BUỘC LÀM TRƯỚC TIÊN)
- **Lệnh thực thi**: `dotnet ef migrations add InitialCreate --project src/WaterbusSystem.Infrastructure --startup-project src/WaterbusSystem.WebApi -o Persistence/Migrations`
- **Điều kiện tiên quyết**: `docker compose up -d` (SQL Server container phải chạy vì `dotnet ef` cần connect để lấy schema hiện có, dù chỉ tạo migration).
- **Sau khi tạo**: chạy thử `dotnet run --project src/WaterbusSystem.WebApi`, xác nhận log `InitialiseAsync` chạy `MigrateAsync()` thành công, `SeedAsync()` chèn được 5 bến/1 tuyến/1 tàu 60 ghế/3 tài khoản vào DB thật.
- **DoD**: Bảng trong SQL Server (`WaterbusDb`) khớp đúng entity hiện có; `GET /api/v1/stations` qua Swagger trả về 5 bến thật từ DB (không phải mock).

### P1-BE-01 — CRUD Bến (Stations)
- **File tạo mới**: `Application/Features/Stations/Commands/CreateStation/CreateStationCommand.cs`, `.../UpdateStation/UpdateStationCommand.cs`, `.../DeleteStation/DeleteStationCommand.cs`
- **Contract**: `CreateStationCommand(string Code, string Name, string Address, double Latitude, double Longitude, int OrderIndex) : IRequest<Guid>`
- **Business rule**: `Code` unique (đã có `HasIndex(x => x.Code).IsUnique()` ở DB — Handler nên pre-check bằng query để trả lỗi rõ ràng thay vì để SQL ném `DbUpdateException` khó đọc). `DeleteStation` là soft-delete (`IsDeleted = true`), phải chặn xoá nếu bến đang được `Route` nào đó tham chiếu làm `DepartureStationId`/`ArrivalStationId` còn active — nếu không sẽ để lại Route "mồ côi".
- **Controller**: mở rộng `StationsController` thêm `[HttpPost]`, `[HttpPut("{id}")]`, `[HttpDelete("{id}")]`, gắn `[Authorize(Roles = "Admin")]`.
- **DoD**: Tạo/sửa/xoá bến qua Swagger với JWT Admin; xoá bến đang bị Route tham chiếu → 409 rõ ràng, không phải 500.

### P1-BE-02 — CRUD Tuyến (Routes)
- **Phụ thuộc**: P1-BE-01 (cần Station Id thật để test)
- **File tạo mới**: `Application/Features/Routes/{Commands,Queries}/...`, `WebApi/Controllers/RoutesController.cs` (chưa tồn tại — phải tạo mới hoàn toàn)
- **Business rule**: `DepartureStationId != ArrivalStationId` (validate bằng FluentValidation, không phải chỉ check ở DB). `Code` unique. Cân nhắc thêm rule: cả 2 Station phải `IsActive = true`.
- **Query cần có**: `GetRoutesQuery` trả về kèm tên 2 bến (giống cách `SearchTripsQuery` include `Route.DepartureStation`/`ArrivalStation`) để Web hiển thị dropdown không cần gọi 2 API.
- **DoD**: CRUD tuyến hoạt động, tạo tuyến trỏ Station không tồn tại → 404/400 rõ ràng (không phải FK constraint exception thô).

### P1-BE-03 — CRUD Tàu & Ghế (Boats & Seats)
- **File tạo mới**: `Application/Features/Boats/...`, `WebApi/Controllers/BoatsController.cs`
- **Business rule quan trọng**: Khi tạo Boat, phải **tổng quát hoá logic sinh ghế** đang nằm cứng trong `ApplicationDbContextInitializer.TrySeedAsync()` (dòng 112-167) thành 1 service/method dùng chung — nhận tham số `int frontCabinSeats, int standardSeats, int outdoorSeats` thay vì hardcode 12/36/12, `SeatCode` sinh theo pattern `{F|S|O}{index:D2}`. **Không copy-paste code seed**, refactor nó thành `IBoatSeatLayoutGenerator` hoặc method static dùng lại được ở cả Seeder lẫn Command mới.
- **Ràng buộc DB đã có sẵn**: `HasIndex(x => new { x.BoatId, x.SeatCode }).IsUnique()` — 1 tàu không trùng mã ghế, generator phải tôn trọng.
- **DoD**: Tạo tàu mới N ghế qua API, `TotalSeats` trên Boat phải khớp đúng tổng số ghế thực sinh ra (validate consistency).

### P1-BE-04 — Lịch chạy cố định (Fixed Schedule Engine)
- **Phụ thuộc**: P1-BE-02, P1-BE-03
- **File tạo mới**: `Application/Features/Schedules/Commands/{CreateSchedule, GenerateTrips}/...`, `WebApi/Controllers/SchedulesController.cs`, thêm `ScheduleConfiguration` vào `EntityConfigurations.cs` (đang thiếu — xem mục 0.4)
- **Thiết kế bắt buộc do Schedule không có BoatId (xem mục 0.5)**: `GenerateTripsCommand(Guid ScheduleId, Guid BoatId, DateOnly FromDate, DateOnly ToDate)` — Admin chọn tàu chạy cho đợt sinh chuyến này, không suy ra tự động.
- **Business rule sinh Trip**:
  - Đọc `Schedule.DaysOfWeek` (chuỗi `"1,2,3,4,5,6,7"`, 1=Thứ 2) → duyệt từng ngày trong khoảng `FromDate..ToDate`, chỉ tạo Trip nếu `(int)date.DayOfWeek` khớp (lưu ý C# `DayOfWeek.Sunday = 0`, phải map đúng sang quy ước "1=Thứ 2" của Schedule, tức `Sunday → "7"`, không phải `"0"` hay `"1"`).
  - `Trip.DepartureTime = ngày cụ thể + Schedule.DepartureTime`, `Trip.ArrivalTime = DepartureTime + Route.EstimatedDurationMinutes`.
  - `Trip.RouteId = Schedule.RouteId` (copy, denormalize để `SearchTripsQuery` filter nhanh không cần join qua Schedule).
  - `Trip.TripType = Schedule.TripType`, `Trip.BasePrice` lấy theo cấu hình giá (nếu chưa có bảng giá riêng, dùng default `15000m` như hiện tại — KHÔNG bịa thêm bảng Pricing nếu spec chưa yêu cầu).
  - **Chặn trùng lặp**: nếu chạy `GenerateTrips` 2 lần cho cùng khoảng ngày → không tạo trùng Trip (check tồn tại theo `ScheduleId + DepartureTime` trước khi insert).
  - **Idempotent theo thiết kế**, vì đây rất có thể được gọi lại nhiều lần khi Admin chỉnh sửa lịch.
- **DoD**: Gọi 1 API sinh ra đúng số Trip theo DaysOfWeek trong khoảng ngày chỉ định; gọi lại lần 2 không tạo trùng; `SearchTripsQuery` tìm thấy các Trip này ngay.

### P1-BE-05 — Hoàn thiện Trip Detail & Booking Lookup Query
- **Phụ thuộc**: P1-BE-04 (cần có Trip thật để test)
- **File tạo mới**: `Application/Features/Trips/Queries/GetTripDetail/GetTripDetailQuery.cs`, `Application/Features/Bookings/Queries/GetBookingByCode/GetBookingByCodeQuery.cs`
- **`GetTripDetailQuery(Guid TripId)`** — trả về **toàn bộ ghế của Boat** kèm trạng thái tính động (không đọc `SeatStatus` field — xem phát hiện #3 mục 0): với mỗi `Seat` thuộc `Trip.Boat`, join với `Tickets.Where(t => t.TripId == TripId && t.SeatId == seat.Id)` để suy ra `Available` (không có ticket Pending/Valid/CheckedIn) / `Held` (có ticket `Pending`) / `Sold` (có ticket `Valid`/`CheckedIn`). Đây chính là dữ liệu P1-WEB-03 (sơ đồ ghế tĩnh) cần.
- **`GetBookingByCodeQuery(string BookingCode)`** — trả `Booking` + danh sách `Ticket` (dùng cho trang tra cứu vé, và cho Incident Self-Service ở Phase 4 sau này).
- **DoD**: Gọi `GetTripDetail` cho 1 Trip đã có người đặt trước → đúng ghế đó hiển thị `Sold`/`Held`, các ghế khác `Available`.

### P1-BE-06 — Auth hoàn thiện
- **File sửa**: `Application/Features/Auth/Commands/Login/LoginCommand.cs` (thêm `LoginCommandHandler` dùng `UserManager`/`SignInManager`/`IJwtTokenGenerator` y hệt logic hiện có trong `AuthController`, chỉ là chuyển vào Handler đúng CQRS)
- **File sửa**: `WebApi/Controllers/AuthController.cs` — đổi `Login` thành `await Mediator.Send(command)`, xoá dependency trực tiếp `UserManager`/`SignInManager` khỏi controller
- **File tạo mới**: `Application/Features/Auth/Commands/Register/RegisterCommand.cs` — chỉ cho phép tạo role `Passenger` qua endpoint public (role Admin/Dispatcher/Staff/Captain/Accountant phải tạo qua Admin User Management, không tự đăng ký được)
- **File tạo mới**: `Infrastructure/Identity/CurrentUserService.cs` implement `ICurrentUserService` (đọc `IHttpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)`/`ClaimTypes.Role` — cần đăng ký `AddHttpContextAccessor()` trong `Infrastructure/DependencyInjection.cs` nếu chưa có)
- **File tạo mới** (Admin only): `Application/Features/Users/Commands/{CreateStaffUser,DeactivateUser}/...`, `Application/Features/Users/Queries/GetUsers/...`
- **DoD**: `Login` đi qua MediatR giống `Bookings`/`Trips`; `Register` tạo được Passenger mới; `ICurrentUserService` trả đúng `UserId`/`UserRole` khi gọi từ 1 endpoint `[Authorize]` bất kỳ.

### P1-BE-07 — Booking cleanup job (huỷ Pending quá hạn)
- **Phụ thuộc**: P1-BE-05
- **File tạo mới**: `Infrastructure/BackgroundJobs/ExpiredBookingCleanupService.cs` implement `BackgroundService` (đủ cho Phase 1 — Hangfire để dành Phase 4 cho các job phức tạp hơn như hoàn tiền)
- **Business rule**: Quét định kỳ (ví dụ mỗi 60s) các `Booking.Status == Pending` có `CreatedAt < UtcNow - 10 phút` → set `Booking.Status = Cancelled`, set từng `Ticket.Status = Cancelled` tương ứng. **Lý do cần job này dù đã có Redis TTL 600s**: Redis lock tự hết hạn chỉ giải phóng được lock ở tầng cache, nhưng **bản ghi `Booking`/`Ticket` ở SQL Server vẫn nằm `Pending` mãi mãi** nếu không có job dọn — ảnh hưởng trực tiếp tới `GetTripDetailQuery` (P1-BE-05) vì nó đang tính `Held` dựa trên `Ticket.Status == Pending`, ghế sẽ bị "kẹt Held" vĩnh viễn nếu không dọn.
- **DoD**: Tạo Booking, không thanh toán, đợi >10 phút → `GetTripDetail` cho thấy ghế đó trở lại `Available`.

### P1-BE-08 — Sửa bug lock-leak ở CreateBookingCommandHandler
- **Phụ thuộc**: không phụ thuộc gì, có thể làm song song với module khác, ưu tiên cao vì là bug thật (mục 0.2)
- **File sửa**: `CreateBookingCommand.cs` — đưa việc release `acquiredLocks` vào khối `finally` áp dụng cho **mọi** exception path (kể cả `DbUpdateConcurrencyException`), thay vì chỉ nằm trong `catch (Exception)` hiện tại.
- **Lưu ý khi sửa**: không release lock trong path **thành công** (không có exception) — vì lock phải sống hết 10 phút để giữ chỗ chờ VNPay IPN xác nhận (đúng như comment cũ ở dòng 503-507 của `SETUP.md` — dự kiến hành vi này, chỉ cần đảm bảo path lỗi luôn release).
- **DoD**: Test giả lập `DbUpdateConcurrencyException` → xác nhận Redis key `lock:trip:*:seat:*` bị xoá ngay lập tức, không phải đợi TTL.

### P1-BE-09 — Hardening còn lại (đã flag từ review trước, chưa xử lý)
- Fix `Program.cs` dòng 86: bỏ `|| true`, chỉ bật Swagger khi `IsDevelopment()` (hoặc thêm flag config riêng cho phép bật ở staging).
- Chuyển `JwtSettings:Secret`, `VnPay:HashSecret`, `ConnectionStrings:DefaultConnection` (SA password) ra khỏi `appsettings.json` sang User Secrets (dev) + biến môi trường (prod). **Không xoá key mặc định trong code** (các chỗ có `?? "..."` fallback) vì đó là an toàn khi thiếu config lúc dev — chỉ đảm bảo file commit không chứa secret thật dùng ở prod.

---

## 3. PHASE 2-4 BACKEND — GIỮ NGUYÊN THEO `TASK_BREAKDOWN_ROADMAP.md`, BỔ SUNG GHI CHÚ KỸ THUẬT

Các module P2-BE-01→04, P3-BE-01→03, P4-BE-01→08 giữ nguyên mô tả trong `TASK_BREAKDOWN_ROADMAP.md`. Bổ sung 2 ghi chú kỹ thuật quan trọng phát hiện thêm khi đọc code:

- **P2-BE-03 (Dynamic TOTP QR)**: `Ticket.QrSeed` **đã tồn tại sẵn** (`Guid.NewGuid().ToString("N")` mặc định khi tạo Ticket, đã có unique constraint qua config `TicketCode` — nhưng `QrSeed` thì chưa có index, không cần unique vì chỉ dùng làm seed sinh TOTP chứ không tra cứu trực tiếp). Việc cần làm ở P2-BE-03 chỉ là **dùng seed có sẵn này** để sinh mã TOTP (RFC 6238) chứ không cần thêm field mới vào `Ticket`.
- **P2-BE-04 (Check-in API)**: `Ticket` đã có sẵn `CheckedInAt` và `CheckedInByStaffId` (chưa dùng) — endpoint checkin chỉ cần set 2 field này + `Status = CheckedIn`, không cần migration thêm field.

---

## 4. THỨ TỰ GIAO VIỆC ĐỀ XUẤT (song song hoá tối đa)

```
Bắt buộc trước tiên (chặn tất cả):
  P1-BE-00 (Migration)

Có thể chạy song song ngay sau P1-BE-00 (không phụ thuộc nhau):
  P1-BE-01 (Stations CRUD)
  P1-BE-06 (Auth hoàn thiện)
  P1-BE-08 (fix lock-leak bug)
  P1-BE-09 (hardening)

Chạy sau khi P1-BE-01 xong:
  P1-BE-02 (Routes CRUD)
  P1-BE-03 (Boats/Seats CRUD) — thực ra độc lập với P1-BE-01, có thể chạy song song luôn

Chạy sau khi P1-BE-02 + P1-BE-03 xong:
  P1-BE-04 (Fixed Schedule Engine)

Chạy sau khi P1-BE-04 xong:
  P1-BE-05 (Trip Detail + Booking Lookup Query)

Chạy sau khi P1-BE-05 xong:
  P1-BE-07 (Booking cleanup job)
```
