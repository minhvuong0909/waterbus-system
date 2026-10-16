# ⚙️ Waterbus Backend (.NET 8 Clean Architecture)

Hệ thống API backend cho nền tảng Smart Waterbus, xây dựng trên nền tảng **.NET 8**, tuân thủ nguyên lý **Clean Architecture**, **CQRS (MediatR)**, **Domain-Driven Design (DDD)** và chuẩn hóa theo bộ quy tắc **Business Rules v2.0 FINAL**.

---

## 🏛️ Kiến Trúc Mã Nguồn (Clean Architecture)

```text
waterbus-be/
├── src/
│   ├── WaterbusSystem.Domain/          # Core Domain (Entities, Enums, Exceptions, Domain Rules)
│   │   ├── Common/                     # BaseEntity, Value Objects, IDomainEvent
│   │   ├── Entities/                   # 33 Entities (PurchaseOrder, Booking, SeatReservation, Ticket, Trip, Station...)
│   │   └── Enums/                      # BoardingStatus, TripStatus, PaymentStatus, UserRole...
│   │
│   ├── WaterbusSystem.Application/     # Use Cases & Application Logic
│   │   ├── Common/                     # Interfaces, DTOs, Behaviors, Global Exceptions
│   │   └── Features/                   # Feature Slices (1 folder = 1 lát cắt nghiệp vụ)
│   │       ├── Bookings/               # Commands/Queries: CreateBooking, CancelBooking, GetBooking...
│   │       ├── Payments/               # Commands/Queries: ProcessVnPayPayment, HandleWebhook...
│   │       ├── Trips/                  # Commands/Queries: SearchTrips, GetTripDetail, GetSeatMap...
│   │       ├── AiBooking/              # AskAiBookingQuery, Validators, TripSearchTool contracts
│   │       ├── SightseeingAudio/       # Geofence Audio Trigger Engine & Evaluation Services
│   │       └── Stations/, Boats/...    # Danh mục bến tàu, đội tàu thủy
│   │
│   ├── WaterbusSystem.Infrastructure/  # External Services & Data Persistence
│   │   ├── Persistence/                # ApplicationDbContext, EntityConfigurations (EF Core Fluent API)
│   │   ├── Services/                   # Redis Lock, VNPay Service, QR Signer, Hangfire Jobs
│   │   └── Services/AiBooking/, Tts/   # Semantic Kernel, Azure Speech TTS, Blob Storage
│   │
│   └── WaterbusSystem.WebApi/          # API Host & Presentation Layer
│       ├── Controllers/                # REST Controllers (kế thừa BaseApiController, chỉ Mediator.Send)
│       ├── Hubs/                       # Real-time SignalR Hubs (/hubs/seat-sync, /hubs/boat-tracking)
│       ├── Middlewares/                # GlobalExceptionHandlingMiddleware (ProblemDetails chuẩn)
│       └── Program.cs                  # Dependency Injection & Middleware Pipeline configuration
│
└── tests/
    ├── WaterbusSystem.UnitTests/       # 450+ Unit Tests bao phủ Domain, Commercial, Security, AI
    └── infrastructure/                 # Kịch bản kiểm thử tích hợp trên Docker SQL Server & Redis
```

---

## 🔒 Quy Tắc Nghiệp Vụ Bất Biến (Core Invariants)

1. **Ranh giới Phụ thuộc**:
   - `Domain` không phụ thuộc vào bất kỳ thư viện ngoài nào (kể cả EF Core hay MediatR).
   - `Application` chỉ phụ thuộc vào `Domain`.
   - `Infrastructure` và `WebApi` phụ thuộc vào `Application` và `Domain`.
2. **Luồng Thực Thể Nghiệp Vụ**:
   - `PurchaseOrder` ➔ `Booking` ➔ `SeatReservation` ➔ `Ticket`.
   - **Ticket chỉ được phát hành sau khi thanh toán thành công** (`Ticket.Issue()`). Không bao giờ tạo vé ở trạng thái Pending.
3. **Giữ Ghế Theo Đoạn (`VisitOrder`)**:
   - Vị trí dừng của tàu được xác định qua `TripStopCall.VisitOrder`, không dùng `Station.OrderIndex`.
   - Chặn chồng lấn chặng ghế (Overlapping segment reservation) trong cùng một cơ sở dữ liệu giao dịch.
4. **Phòng Thủ Chống Đặt Trùng 2 Lớp (Double-Booking Protection)**:
   - **Lớp 1**: Redis Distributed Lock với thời gian hết hạn (TTL 10 phút). Luôn giải phóng lock ở nhánh lỗi; giữ lock ở nhánh thành công cho đến khi thanh toán hoàn tất.
   - **Lớp 2**: EF Core Optimistic Concurrency qua `RowVersion` (`byte[]`).
5. **Giao Dịch Tài Chính & Hoàn Tiền**:
   - Mọi khoản thu và hoàn tiền đều lưu qua thực thể `FinancialTransaction` với khóa định danh duy nhất (`IdempotencyKey`).
   - So khớp chữ ký bảo mật bằng hàm chống tấn công định thời: `CryptographicOperations.FixedTimeEquals`.
6. **Xử Lý Lỗi Chuẩn Hóa**:
   - Ném ngoại lệ nghiệp vụ miền (`ValidationException`, `NotFoundException`, `SeatAlreadyBookedException`, `ConcurrencyException`).
   - `GlobalExceptionHandlingMiddleware` tự động chuyển đổi sang định dạng chuẩn RFC 7807 **ProblemDetails** (mã lỗi 400, 404, 409, 500 kèm `traceId`).

---

## 🛠️ Hướng Dẫn Phát Triển & Kiểm Thử (Build & Test)

### 1. Yêu cầu môi trường
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (hoặc Docker SQL Server 2022)
- Redis 7 (hoặc Docker Redis)

### 2. Cài đặt & Build
```powershell
cd waterbus-be
dotnet restore WaterbusSystem.sln
dotnet build WaterbusSystem.sln
```

### 3. Chạy Unit Tests
Toàn bộ unit test chạy độc lập không phụ thuộc cloud bên ngoài:
```powershell
dotnet test WaterbusSystem.sln
```

### 4. EF Core Migrations
Khi thêm hoặc thay đổi cấu hình thực thể trong `EntityConfigurations.cs`:
```powershell
dotnet ef migrations add <TenMigration> --project src/WaterbusSystem.Infrastructure --startup-project src/WaterbusSystem.WebApi
dotnet ef database update --project src/WaterbusSystem.Infrastructure --startup-project src/WaterbusSystem.WebApi
```

### 5. Cấu hình Bí Mật Cục Bộ (User Secrets)
Không commit mật khẩu, khóa bí mật hoặc connection string vào Git:
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1,11433;Database=WaterbusDb;User Id=sa;Password=<Pass>;TrustServerCertificate=True" --project src/WaterbusSystem.WebApi
dotnet user-secrets set "ConnectionStrings:RedisConnection" "127.0.0.1:16379" --project src/WaterbusSystem.WebApi
dotnet user-secrets set "JwtSettings:Secret" "<Khoa_Bao_Mat_JWT_Toi_Thieu_32_Ky_Tu>" --project src/WaterbusSystem.WebApi
```

---

## 📡 Tích Hợp Real-time & Nền tảng AI

- **SignalR Hubs**:
  - `/hubs/seat-sync`: Đồng bộ trạng thái ghế theo thời gian thực (`seatHeld`, `seatReleased`, `seatSold`).
  - `/hubs/boat-tracking`: Cập nhật vị trí GPS của tàu, cảnh báo thời tiết và sự cố.
- **AI Booking Assistant**: Tích hợp Semantic Kernel với OpenAI (`gpt-4.1-mini`), trích xuất Trip Card thực tế từ hệ thống, loại bỏ ảo giác thông tin.
- **Sightseeing Audio Geofence**: Đánh giá trắc địa NetTopologySuite, kích hoạt loa thuyết minh du lịch khi tàu di chuyển vào bán kính POI.
