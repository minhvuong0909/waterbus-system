# AGENTS.md — Quy tắc bắt buộc cho AI coding agent (Claude Code, Codex, Antigravity, ...)

Dự án **Smart Waterbus**: đặt vé tàu buýt đường sông + du lịch (PRN232). Đọc file này TRƯỚC khi sửa code.
Chi tiết đầy đủ: `docs/CONVENTIONS.md`. Nguồn chuẩn nghiệp vụ: `docs/Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md` (BR v2.0).
Nếu `SETUP.md` / `docs/TASK_BREAKDOWN_ROADMAP.md` mâu thuẫn với BR v2.0 hoặc file này → **BR v2.0 và file này thắng** (hai file kia viết trước BR v2.0).

## Cấu trúc
- `waterbus-be/` .NET 8 Web API, Clean Architecture (Domain, Application, Infrastructure, WebApi) + `tests/WaterbusSystem.UnitTests`
- `waterbus-fe/` Web React + Vite + TypeScript (Customer/Guest, Admin, Captain)
- `waterbus-mobile/` CHỈ app Staff Scanner
- `docker-compose.yml` (gốc) chạy cả hệ thống; `docs/` tài liệu

## Lệnh
```bash
cd waterbus-be && dotnet build WaterbusSystem.sln && dotnet test WaterbusSystem.sln   # BE
cd waterbus-fe && npm ci && npm run build                                              # FE (gồm tsc)
docker compose up -d --build                                                           # cả hệ thống, Web ở :8080
```
Hiện có **4 test `CreateBookingCommandHandlerTests` đang fail** (đã biết). Không được làm thêm test nào fail.
Test cần SQL thật: đặt `WATERBUS_TEST_SQLSERVER` (tự tạo/xoá DB `WaterbusDomainTests_*`).
Luôn chạy build + test liên quan trước khi báo hoàn thành; báo trung thực nếu có fail.

## Nghiệp vụ (không được vi phạm)
- 5 actor: Passenger, Guest, Admin, Staff, Captain. **Không có Dispatcher.** Guest KHÔNG có bảng riêng, truy cập qua `AccessGrant` (chỉ lưu `TokenHash`, không lưu token gốc).
- Luồng: `PurchaseOrder → Booking → SeatReservation → Ticket`. **Ticket chỉ phát sau thanh toán thành công** (`Ticket.Issue()`).
- Ghế giữ theo **đoạn**, dùng `VisitOrder` của `TripStopCall`, KHÔNG dùng `Station.OrderIndex`. DB không có unique (Trip, Seat) → chặn chồng lấn đoạn do service làm trong transaction.
- Chống đặt trùng 2 lớp: Redis lock (TTL 10 phút) + RowVersion. Luôn release lock ở nhánh lỗi; nhánh thành công giữ lock đến khi thanh toán xác nhận/hết TTL.
- Payment và Refund dùng chung `FinancialTransaction`; mọi giao dịch có `IdempotencyKey`; so chữ ký bằng `CryptographicOperations.FixedTimeEquals`.
- **Incident không tự đổi `Trip.Status`.** Admin quyết định qua `IncidentTrip.OperationDecision`. Mỗi lần đổi `Trip.Status` phải ghi 1 `TripOperationEvent`.
- Không dùng code legacy trong code mới: `PaymentTransaction`, `Booking.TotalAmount`, `TicketStatus.Pending/CheckedIn/Refunded`. Dùng `BoardingStatus` cho check-in.

## Backend
- Chiều phụ thuộc: WebApi→Application→Domain; Infrastructure→Application→Domain. Domain không tham chiếu EF/MediatR/hạ tầng.
- **1 file = 1 feature slice**: `Application/Features/{Feature}/{Commands|Queries}/{Name}/{Name}{Command|Query}.cs` chứa DTO + Command/Query + `AbstractValidator` + Handler. Mẫu: `CreateBookingCommand.cs`, `SearchTripsQuery.cs`.
- Controller kế thừa `BaseApiController`, chỉ `Mediator.Send(...)`, KHÔNG có logic nghiệp vụ. Mọi endpoint không công khai phải có `[Authorize(Roles=...)]`.
- Lỗi: **ném exception**, không tự `return BadRequest/Conflict`. Map ở `GlobalExceptionHandlingMiddleware` (ProblemDetails): ValidationException 400, NotFoundException 404, ConcurrencyException/SeatAlreadyBookedException 409, còn lại 500. Cần mã mới → thêm `case` vào middleware.
- EF Core: entity kế thừa `BaseEntity`; mỗi entity có `IEntityTypeConfiguration<T>` trong `EntityConfigurations.cs` (khoá, unique mã nghiệp vụ, độ dài chuỗi, `RowVersion.IsRowVersion()`, `DeleteBehavior` tường minh, mặc định `Restrict`). Query chỉ đọc dùng `AsNoTracking()`. Xoá danh mục = soft delete (`IsDeleted`), query lọc `!IsDeleted`; không `Remove()` entity nghiệp vụ. Ưu tiên check constraint/composite FK ở DB cho bất biến.
- Thời gian `DateTimeOffset` lưu UTC. Tiền `decimal(12,2)`, toạ độ `decimal(10,7)`. Không đổi số của enum đã có.
- Migration: mỗi lần một người; tên mô tả (`AddBoatPositionIndex`); KHÔNG sửa migration đã merge; migration có thể phá dữ liệu phải có bước kiểm tra trước. Lệnh:
  `dotnet ef migrations add <Name> --project src/WaterbusSystem.Infrastructure --startup-project src/WaterbusSystem.WebApi`
- Đặt tên: PascalCase, field private `_camelCase`, interface `I...`; `...Command`, `...Query`, `...Handler`, `...Validator`. Identifier tiếng Anh; comment/XML-doc tiếng Việt như code hiện tại.
- Test: thêm vào `tests/WaterbusSystem.UnitTests/<Module>/`; mỗi rule/handler mới có test đường chính + 1 lỗi + 1 cạnh biên.

## Bảo mật
- KHÔNG commit secret (`.env`, JWT secret, VNPay secret, mật khẩu). Local dùng `dotnet user-secrets`; deploy dùng biến môi trường (`ConnectionStrings__DefaultConnection`, `JwtSettings__Secret`...). Key cấu hình mới → cập nhật cả `appsettings.example.json` và `.env.example`.
- Swagger chỉ bật ở Development. Không log email/SĐT/token/payload thanh toán đầy đủ.

## Real-time (SignalR)
- Hub ở `WebApi/Hubs/`: `/hubs/seat-sync`, `/hubs/boat-tracking`. Application chỉ phụ thuộc interface (`ISeatEventPublisher`, `IBoatTrackingPublisher`); **handler không tham chiếu Hub trực tiếp**.
- Group: `trip:{tripId}`, `boat:{boatId}`, `fleet`. Event camelCase, payload là DTO:
  `seatHeld|seatReleased|seatSold {tripId, seatId, boardingVisitOrder, disembarkingVisitOrder, status, expiresAt?}`,
  `boatPosition {boatId, tripId?, lat, lng, speedKnots?, observedAt}`,
  `weatherAlert {incidentId, severity, routeId?, message, occurredAt}`.
- Hub không quyết định nghiệp vụ; giữ/nhả ghế vẫn đi qua REST. JWT qua query `access_token`. GPS chỉ role Captain gửi; radar chỉ Admin; hub ghế cho Guest chỉ đọc. Client phải gọi lại REST để đồng bộ sau khi reconnect.

## Background job
- Hangfire (SQL Server); dashboard `/hangfire` chỉ role Admin. Job phải **idempotent** (không tạo Incident trùng cho cùng tuyến + khoảng thời gian). Weather Guard chỉ tạo `Incident` (`Source="WeatherService"`, `IncidentType="Weather"`). Ngưỡng, chu kỳ, API key đặt trong cấu hình. Gọi dịch vụ ngoài có timeout + retry giới hạn; lỗi chỉ log, không làm sập host.

## Frontend
- Giữ cấu trúc `pages/ features/ components/ layouts/ routes/ store/ hooks/ constant/`. Mọi API đi qua `constant/apiEndpoints.ts` + service trong `features/<x>/services`; không `fetch` rải rác. Biến môi trường có tiền tố `VITE_` và có `.env.example`.
- Gửi `Accept-Language`. Dùng đường dẫn tương đối `/api/...` và `/hubs/...` (nginx proxy cùng origin). Guest dùng link bảo mật, không bắt đăng nhập. SignalR bọc trong 1 service (auto reconnect, join lại group).
- Tiền định dạng `vi-VN`; thời gian nhận UTC, hiển thị giờ Việt Nam. Component `PascalCase.tsx`, hook `useXxx`, tránh `any`. `npm run build` phải qua.

## Mobile (Staff Scanner)
- Verify QR **offline** bằng Public Key, không gọi API mỗi lần quét. Mỗi check-in có `clientEventId` duy nhất, đồng bộ theo lô (server khử trùng bằng `CheckInEvent.ClientEventId`). Chỉ check-in trong `CheckInOpenAt–CheckInCloseAt` của đúng `TripStopCall` được phân công.

## API
- `/api/v1/...`, JSON camelCase, id là GUID, lỗi luôn là ProblemDetails (có `traceId`, validate trong `errors`). Danh sách lớn phải phân trang (`page`, `pageSize`). Đổi hợp đồng API/event → cập nhật Swagger + tài liệu trước khi merge (FE/mobile/AI phụ thuộc).

## Git
- Không push trực tiếp `main`. Nhánh `feature/<tên>-<việc>`, `fix/...`, `docs/...`. Commit dạng Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`). 1 PR = 1 mục đích; cần build + test xanh và ≥1 review.
- Không commit: secrets, `bin/`, `obj/`, `node_modules/`, `.env`.

## Cách làm việc của agent
- Làm đúng phạm vi được giao; không refactor lan man, không sửa file của module khác nếu không cần. Nếu việc buộc phải đổi file của người khác (entity, EF config, hợp đồng API) → nêu rõ trong mô tả.
- Đọc code mẫu cạnh bên và viết giống phong cách hiện có trước khi thêm file mới.
- Không phát minh nghiệp vụ: thiếu thông tin thì tra BR v2.0; vẫn thiếu thì hỏi người dùng.
- Không bịa kết quả test/build. Chưa chạy thì nói chưa chạy.

## Definition of Done
Build + test liên quan pass, không thêm test fail · tuân thủ BR v2.0 (nhất là phần tiền/ghế) · có validate + lỗi qua exception chuẩn · endpoint có phân quyền · không secret trong diff · cập nhật Swagger/tài liệu nếu đổi API/event · đã tự chạy thử thật (Swagger/UI) không chỉ unit test.
