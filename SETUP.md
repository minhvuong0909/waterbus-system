# BACKEND SYSTEM SETUP MANUAL
## DỰ ÁN: SMART WATERBUS (.NET 8 CLEAN ARCHITECTURE)
> **Dành cho Codex / AI Developer**: Bản thiết kế và hướng dẫn thực thi setup chuyên biệt cho **Backend .NET 8 Web API** theo chuẩn Clean Architecture, CQRS, EF Core 8, Identity JWT, VNPAY HMAC-SHA512. Tối ưu hiệu năng, bảo mật chống Timing Attack, và **triển khai triệt để 2 lớp phòng thủ chống Double-booking (Redis RedLock + EF Core Optimistic Concurrency)**.

---

## 1. KIẾN TRÚC TỔNG QUAN & CƠ CHẾ BẢO VỆ 2 LỚP (DEFENSE IN DEPTH)

```
                            ┌─────────────────────────────────────────┐
                            │        WaterbusSystem.WebApi            │
                            │  - REST Controllers (v1)               │
                            │  - Middlewares (Exception, i18n)        │
                            │  - Swagger JWT Authorization            │
                            └────────────────────┬────────────────────┘
                                                 │
                   ┌─────────────────────────────┴─────────────────────────────┐
                   ▼                                                           ▼
┌─────────────────────────────────────────┐                 ┌─────────────────────────────────────────┐
│     WaterbusSystem.Infrastructure       │                 │       WaterbusSystem.Application        │
│  - EF Core 8 + SQL Server Provider      │                 │  - CQRS Commands & Queries (MediatR)    │
│  - Entity Configurations (Fluent API)   │◄────────────────┤  - Pipeline Behaviors (Validation, Log) │
│  - IDistributedLockService (RedLock.net)│                 │  - FluentValidation Rules               │
│  - ASP.NET Core Identity & JWT Service  │                 │  - Core Interfaces & DTOs               │
│  - VNPAY Gateway (HMAC-SHA512 +         │                 │  - IDistributedLockService Interface    │
│    CryptographicOperations.FixedTime)   │                 │                                         │
│  - Initializer & Database Seeding       │                 │                                         │
└────────────────────┬────────────────────┘                 └────────────────────┬────────────────────┘
                     │                                                           │
                     └─────────────────────────────┬─────────────────────────────┘
                                                   ▼
                                ┌─────────────────────────────────────────┐
                                │          WaterbusSystem.Domain          │
                                │  - Enterprise Business Entities         │
                                │  - BaseEntity CÓ RowVersion (byte[])    │
                                │  - Enums (Trip, Seat, Ticket, Payment)  │
                                │  - Domain Exceptions & Base Models      │
                                └─────────────────────────────────────────┘
```

### 🛡️ CHI TIẾT 2 LỚP PHÒNG THỦ CHỐNG DOUBLE-BOOKING / OVERSELLING

```
Client Gửi Request Giữ Chỗ (TripId, SeatId)
                 │
                 ▼
[ LỚP 1: REDIS REDLOCK ] ──(Khóa key: "lock:seat:{TripId}:{SeatId}" - TTL: 10 phút)
                 │
                 ├──► Không lấy được Lock ──► Trả về 409 Conflict: "Ghế đang được người khác giữ"
                 ▼
          Lấy Lock Thành Công
                 │
                 ▼
[ LỚP 2: EF CORE OPTIMISTIC CONCURRENCY ]
                 │
                 ├──► Kiểm tra DB: SeatStatus == Available
                 ├──► Cập nhật SeatStatus = Held
                 ├──► DbContext.SaveChangesAsync() ──(Kiểm tra RowVersion trên SQL Server)
                 │         │
                 │         └──► DbUpdateConcurrencyException ──► Rollback + Release Redis Lock + 409 Conflict
                 ▼
     Lưu DB Thành Công (RowVersion tự tăng)
                 │
                 ▼
   Sinh Countdown Timer 10 Phút & Trả về Booking Pending
```

---

## 2. CẤU TRÚC THƯ MỤC BACKEND CHUẨN HOÁ

```text
backend/
├── WaterbusSystem.sln
├── docker-compose.yml                       # Chạy SQL Server 2022 & Redis 7
├── src/
│   ├── WaterbusSystem.Domain/               # LỚP 1: CORE DOMAIN
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs                # Id (Guid), CreatedAt, UpdatedAt, IsDeleted, RowVersion (byte[])
│   │   │   └── IAuditableEntity.cs
│   │   ├── Entities/
│   │   │   ├── Station.cs                   # Bến tàu (Code, Name, Lat, Lng, OrderIndex)
│   │   │   ├── Route.cs                     # Tuyến (DepartureStation, ArrivalStation, Duration)
│   │   │   ├── Boat.cs                      # Tàu (Code, Name, RegistrationNo, TotalSeats)
│   │   │   ├── Seat.cs                      # Ghế (BoatId, SeatCode, Category, Row, Col)
│   │   │   ├── Schedule.cs                  # Lịch chạy cố định (RouteId, DepartureTime, DaysOfWeek)
│   │   │   ├── Trip.cs                      # Chuyến thực tế (ScheduleId, BoatId, Date, Status, Price)
│   │   │   ├── Booking.cs                   # Đơn đặt (BookingCode, Customer, TotalAmount, Status)
│   │   │   ├── Ticket.cs                    # Vé (BookingId, TripId, SeatId, TicketCode, QrSeed)
│   │   │   └── PaymentTransaction.cs        # Giao dịch (BookingId, TxnCode, VNPAY Response, Hash)
│   │   ├── Enums/
│   │   │   ├── TripType.cs                  # Commuter = 1, Sightseeing = 2
│   │   │   ├── TripStatus.cs                # Scheduled, Boarding, InTransit, Completed, Cancelled
│   │   │   ├── SeatCategory.cs              # FrontCabin = 1, Standard = 2, Outdoor = 3
│   │   │   ├── SeatStatus.cs                # Available = 1, Held = 2, Sold = 3, Blocked = 4
│   │   │   ├── TicketStatus.cs              # Valid = 1, CheckedIn = 2, Cancelled = 3, Refunded = 4
│   │   │   ├── PaymentStatus.cs             # Pending = 1, Success = 2, Failed = 3, Refunded = 4
│   │   │   └── UserRole.cs                  # Admin, Dispatcher, Accountant, Captain, Staff, Passenger
│   │   └── Exceptions/
│   │       ├── DomainException.cs
│   │       ├── SeatAlreadyBookedException.cs
│   │       └── NotFoundException.cs
│   │
│   ├── WaterbusSystem.Application/          # LỚP 2: APPLICATION & CQRS
│   │   ├── Common/
│   │   │   ├── Behaviors/
│   │   │   │   ├── ValidationBehavior.cs    # Tự động validate bằng FluentValidation qua MediatR
│   │   │   │   └── LoggingBehavior.cs       # Ghi log thời gian thực thi request
│   │   │   ├── Exceptions/
│   │   │   │   ├── ValidationException.cs
│   │   │   │   └── ConcurrencyException.cs
│   │   │   ├── Interfaces/
│   │   │   │   ├── IApplicationDbContext.cs # Khai báo DbSet<T> và SaveChangesAsync
│   │   │   │   ├── IDistributedLockService.cs # Khai báo AcquireLockAsync & ReleaseLockAsync (REDIS)
│   │   │   │   ├── IJwtTokenGenerator.cs    # Khai báo GenerateToken
│   │   │   │   ├── IVnPayService.cs         # Khai báo CreatePaymentUrl, ValidateSignature
│   │   │   │   └── ICurrentUserService.cs   # Lấy UserId, UserRole từ HttpContext
│   │   │   └── Models/
│   │   │       └── PaginatedList.cs
│   │   ├── Features/
│   │   │   ├── Auth/
│   │   │   ├── Stations/
│   │   │   ├── Routes/
│   │   │   ├── Boats/
│   │   │   ├── Trips/
│   │   │   ├── Bookings/
│   │   │   │   ├── Commands/CreateBooking/  # Thực hiện 2 lớp: RedLock -> EF Core RowVersion
│   │   │   │   └── Queries/GetBookingByCode/
│   │   │   └── Payments/
│   │   └── DependencyInjection.cs
│   │
│   ├── WaterbusSystem.Infrastructure/       # LỚP 3: INFRASTRUCTURE & EXTERNAL
│   │   ├── Identity/
│   │   │   ├── ApplicationUser.cs           # Kế thừa IdentityUser<Guid>
│   │   │   ├── ApplicationRole.cs           # Kế thừa IdentityRole<Guid>
│   │   │   └── JwtTokenGenerator.cs         # Cấp phát JWT token HMAC-SHA256
│   │   ├── Locking/                         # Triển khai Redis RedLock
│   │   │   └── RedisDistributedLockService.cs # Thực thi IDistributedLockService dùng RedLock.net / StackExchange.Redis
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs      # Kế thừa IdentityDbContext, implement IApplicationDbContext
│   │   │   ├── ApplicationDbContextInitializer.cs # Chạy Migration & Seed 5 bến, 1 tàu 60 ghế, accounts
│   │   │   └── Configurations/              # Fluent API cấu hình từng Entity (Index, Precision, RowVersion)
│   │   ├── Services/
│   │   │   ├── DateTimeService.cs
│   │   │   └── Payment/
│   │   │       ├── VnPayService.cs          # Thuật toán sinh URL & verify chữ ký bằng CryptographicOperations.FixedTimeEquals
│   │   │       └── VnPayAsciiComparer.cs    # Sắp xếp tham số ASCII Alphabetical theo quy định VNPAY
│   │   └── DependencyInjection.cs           # Đăng ký DbContext, Redis, Identity, JWT, Services
│   │
│   └── WaterbusSystem.WebApi/               # LỚP 4: REST API ENTRYPOINT
│       ├── Controllers/
│       │   ├── BaseApiController.cs
│       │   ├── AuthController.cs
│       │   ├── StationsController.cs
│       │   ├── RoutesController.cs
│       │   ├── BoatsController.cs
│       │   ├── TripsController.cs
│       │   ├── BookingsController.cs        # Chống overselling
│       │   └── PaymentsController.cs        # Webhook IPN an toàn
│       ├── Middlewares/
│       │   ├── GlobalExceptionHandlingMiddleware.cs # Bắt ConcurrencyException trả về 409 Conflict
│       │   └── RequestLocalizationMiddleware.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── Program.cs
│
└── tests/
    └── WaterbusSystem.UnitTests/
        ├── Security/
        │   └── VnPaySecurityTests.cs        # Verify FixedTimeEquals & HMAC-SHA512
        ├── Concurrency/
        │   └── DistributedLockTests.cs      # Test AcquireLock & ReleaseLock
        └── Features/
            ├── Trips/SearchTripsQueryTests.cs
            └── Bookings/CreateBookingValidatorTests.cs
```

---

## 3. LỆNH CLI THỰC THI SETUP CHO CODEX (POWERSHELL / BASH)

Codex hãy thực hiện đúng theo chuỗi lệnh CLI sau:

```bash
# 1. Đi vào thư mục backend
cd backend

# 2. Tạo Solution
dotnet new sln -n WaterbusSystem

# 3. Tạo 5 Projects thành phần
dotnet new classlib -n WaterbusSystem.Domain -o src/WaterbusSystem.Domain
dotnet new classlib -n WaterbusSystem.Application -o src/WaterbusSystem.Application
dotnet new classlib -n WaterbusSystem.Infrastructure -o src/WaterbusSystem.Infrastructure
dotnet new webapi -n WaterbusSystem.WebApi -o src/WaterbusSystem.WebApi --no-openapi false
dotnet new xunit -n WaterbusSystem.UnitTests -o tests/WaterbusSystem.UnitTests

# 4. Gắn kết Projects vào Solution
dotnet sln add src/WaterbusSystem.Domain/WaterbusSystem.Domain.csproj
dotnet sln add src/WaterbusSystem.Application/WaterbusSystem.Application.csproj
dotnet sln add src/WaterbusSystem.Infrastructure/WaterbusSystem.Infrastructure.csproj
dotnet sln add src/WaterbusSystem.WebApi/WaterbusSystem.WebApi.csproj
dotnet sln add tests/WaterbusSystem.UnitTests/WaterbusSystem.UnitTests.csproj

# 5. Thiết lập Project References (Quy tắc phụ thuộc 1 chiều)
dotnet add src/WaterbusSystem.Application/WaterbusSystem.Application.csproj reference src/WaterbusSystem.Domain/WaterbusSystem.Domain.csproj
dotnet add src/WaterbusSystem.Infrastructure/WaterbusSystem.Infrastructure.csproj reference src/WaterbusSystem.Application/WaterbusSystem.Application.csproj
dotnet add src/WaterbusSystem.WebApi/WaterbusSystem.WebApi.csproj reference src/WaterbusSystem.Application/WaterbusSystem.Application.csproj
dotnet add src/WaterbusSystem.WebApi/WaterbusSystem.WebApi.csproj reference src/WaterbusSystem.Infrastructure/WaterbusSystem.Infrastructure.csproj
dotnet add tests/WaterbusSystem.UnitTests/WaterbusSystem.UnitTests.csproj reference src/WaterbusSystem.Application/WaterbusSystem.Application.csproj
dotnet add tests/WaterbusSystem.UnitTests/WaterbusSystem.UnitTests.csproj reference src/WaterbusSystem.Domain/WaterbusSystem.Domain.csproj

# 6. Cài đặt NuGet Packages chính xác:
# Application Layer:
dotnet add src/WaterbusSystem.Application package MediatR --version 12.4.1
dotnet add src/WaterbusSystem.Application package FluentValidation --version 11.10.0
dotnet add src/WaterbusSystem.Application package FluentValidation.DependencyInjectionExtensions --version 11.10.0
dotnet add src/WaterbusSystem.Application package Microsoft.EntityFrameworkCore --version 8.0.10

# Infrastructure Layer (Bổ sung RedLock.net & StackExchange.Redis):
dotnet add src/WaterbusSystem.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.10
dotnet add src/WaterbusSystem.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 8.0.10
dotnet add src/WaterbusSystem.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.10
dotnet add src/WaterbusSystem.Infrastructure package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.10
dotnet add src/WaterbusSystem.Infrastructure package System.IdentityModel.Tokens.Jwt --version 8.1.2
dotnet add src/WaterbusSystem.Infrastructure package StackExchange.Redis --version 2.8.16
dotnet add src/WaterbusSystem.Infrastructure package RedLock.net --version 2.3.2
dotnet add src/WaterbusSystem.Infrastructure package Microsoft.Extensions.Options.ConfigurationExtensions --version 8.0.0

# WebApi Layer:
dotnet add src/WaterbusSystem.WebApi package Microsoft.EntityFrameworkCore.Tools --version 8.0.10
dotnet add src/WaterbusSystem.WebApi package Swashbuckle.AspNetCore --version 6.9.0

# UnitTests Layer:
dotnet add tests/WaterbusSystem.UnitTests package FluentAssertions --version 6.12.1
dotnet add tests/WaterbusSystem.UnitTests package Moq --version 4.20.72
```

---

## 4. CODE CONTRACTS & CHI TIẾT CÁC THÀNH PHẦN CỐT LÕI

### 4.1. File `docker-compose.yml` (Nằm tại `backend/docker-compose.yml`)
```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: waterbus_sqlserver
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=Waterbus@StrongP@ss2026!
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql/data
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    container_name: waterbus_redis
    ports:
      - "6379:6379"
    restart: unless-stopped

volumes:
  sqlserver_data:
```

### 4.2. File `backend/src/WaterbusSystem.WebApi/appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=WaterbusDb;User Id=sa;Password=Waterbus@StrongP@ss2026!;TrustServerCertificate=True;MultipleActiveResultSets=true",
    "RedisConnection": "localhost:6379"
  },
  "JwtSettings": {
    "Secret": "WaterbusSystemSuperSecretKeyWith256BitsMinimumLength2026!",
    "Issuer": "WaterbusSystemApi",
    "Audience": "WaterbusClients",
    "ExpiryMinutes": 1440
  },
  "VnPay": {
    "TmnCode": "2QXUI4J4",
    "HashSecret": "RAOCTJRQAXNSYJXXTGZAHQUHIEUXVPUK",
    "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    "ReturnUrl": "http://localhost:5173/payment/callback",
    "Version": "2.1.0"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

### 4.3. BaseEntity với Optimistic Concurrency Token (`RowVersion`)
File `backend/src/WaterbusSystem.Domain/Common/BaseEntity.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace WaterbusSystem.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Optimistic Concurrency Token (Lớp phòng thủ 2 chống Double-booking tại Database level).
    /// EF Core sẽ tự động kiểm tra token này trong câu lệnh WHERE khi UPDATE.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
```

### 4.4. Interface & Implementation Redis Distributed Lock (Lớp Phòng Thủ 1)

#### Interface: `backend/src/WaterbusSystem.Application/Common/Interfaces/IDistributedLockService.cs`
```csharp
namespace WaterbusSystem.Application.Common.Interfaces;

public interface IDistributedLockService
{
    /// <summary>
    /// Thử lấy distributed lock trên Redis với timeout xác định.
    /// </summary>
    /// <param name="resourceKey">Key định danh tài nguyên (ví dụ: lock:seat:tripId:seatId)</param>
    /// <param name="expiryTime">Thời gian giữ lock tối đa trước khi tự release</param>
    /// <param name="waitTime">Thời gian chờ tối đa để lấy lock</param>
    /// <param name="retryTime">Chu kỳ thử lại</param>
    /// <returns>Đối tượng IAsyncDisposable để giải phóng lock khi hoàn tất</returns>
    Task<IAsyncDisposable?> AcquireLockAsync(
        string resourceKey, 
        TimeSpan expiryTime, 
        TimeSpan waitTime, 
        TimeSpan retryTime, 
        CancellationToken cancellationToken = default);
}
```

#### Implementation: `backend/src/WaterbusSystem.Infrastructure/Locking/RedisDistributedLockService.cs`
```csharp
using System.Net;
using Microsoft.Extensions.Configuration;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Locking;

public class RedisDistributedLockService : IDistributedLockService, IDisposable
{
    private readonly RedLockFactory _redLockFactory;

    public RedisDistributedLockService(IConfiguration configuration)
    {
        var redisConnString = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";
        var endPoints = new List<RedLockEndPoint>
        {
            new(new DnsEndPoint(redisConnString.Split(':')[0], int.Parse(redisConnString.Split(':')[1])))
        };
        _redLockFactory = RedLockFactory.Create(endPoints);
    }

    public async Task<IAsyncDisposable?> AcquireLockAsync(
        string resourceKey, 
        TimeSpan expiryTime, 
        TimeSpan waitTime, 
        TimeSpan retryTime, 
        CancellationToken cancellationToken = default)
    {
        var redLock = await _redLockFactory.CreateLockAsync(resourceKey, expiryTime, waitTime, retryTime, cancellationToken);
        return redLock.IsAcquired ? redLock : null;
    }

    public void Dispose()
    {
        _redLockFactory.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

---

### 4.5. Triển khai CreateBooking Command với 2 Lớp Phòng Thủ Chống Double-Booking

File `backend/src/WaterbusSystem.Application/Features/Bookings/Commands/CreateBooking/CreateBookingCommandHandler.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;

public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedLockService _lockService;

    public CreateBookingCommandHandler(IApplicationDbContext context, IDistributedLockService lockService)
    {
        _context = context;
        _lockService = lockService;
    }

    public async Task<BookingDto> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        // -------------------------------------------------------------
        // LỚP PHÒNG THỦ 1: REDIS REDLOCK (Khóa phân tán ở tầng Cache/RAM)
        // -------------------------------------------------------------
        var acquiredLocks = new List<IAsyncDisposable>();
        try
        {
            foreach (var seatId in request.SeatIds)
            {
                var lockKey = $"lock:trip:{request.TripId}:seat:{seatId}";
                // Thử lấy lock trong 2 giây, giữ lock 10 phút (600s), retry mỗi 100ms
                var lockHandle = await _lockService.AcquireLockAsync(
                    lockKey, 
                    expiryTime: TimeSpan.FromMinutes(10), 
                    waitTime: TimeSpan.FromSeconds(2), 
                    retryTime: TimeSpan.FromMilliseconds(100), 
                    cancellationToken);

                if (lockHandle == null)
                {
                    throw new ConcurrencyException($"Ghế {seatId} đang được người khác giữ chỗ hoặc thanh toán. Vui lòng chọn ghế khác!");
                }
                acquiredLocks.Add(lockHandle);
            }

            // -------------------------------------------------------------
            // LỚP PHÒNG THỦ 2: EF CORE OPTIMISTIC CONCURRENCY (Kiểm tra DB)
            // -------------------------------------------------------------
            var trip = await _context.Trips
                .Include(t => t.Seats)
                .FirstOrDefaultAsync(t => t.Id == request.TripId && !t.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(Trip), request.TripId);

            // Kiểm tra trạng thái ghế
            var existingTickets = await _context.Tickets
                .Where(t => t.TripId == request.TripId && request.SeatIds.Contains(t.SeatId) && 
                            (t.Status == TicketStatus.Valid || t.Status == TicketStatus.CheckedIn))
                .ToListAsync(cancellationToken);

            if (existingTickets.Count != 0)
            {
                throw new ConcurrencyException("Một trong các ghế bạn chọn đã được bán trước đó!");
            }

            // Tạo Booking & Tickets
            var booking = new Booking
            {
                BookingCode = $"WB{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Status = BookingStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                TotalAmount = trip.BasePrice * request.SeatIds.Count
            };

            foreach (var seatId in request.SeatIds)
            {
                booking.Tickets.Add(new Ticket
                {
                    TripId = request.TripId,
                    SeatId = seatId,
                    TicketCode = $"TK{Random.Shared.Next(10000000, 99999999)}",
                    Price = trip.BasePrice,
                    PassengerName = request.CustomerName,
                    Status = TicketStatus.Pending
                });
            }

            _context.Bookings.Add(booking);

            // Nếu 2 request lọt qua cùng lúc, câu lệnh SaveChangesAsync với RowVersion
            // sẽ ném DbUpdateConcurrencyException tại Database level
            await _context.SaveChangesAsync(cancellationToken);

            return new BookingDto(booking.Id, booking.BookingCode, booking.TotalAmount, booking.Status.ToString());
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Xung đột dữ liệu khi giữ chỗ. Vui lòng thử lại!", ex);
        }
        finally
        {
            // Nếu phát sinh lỗi trong quá trình tạo DB, giải phóng toàn bộ Redis lock ngay lập tức
            // Nếu thành công, Redis lock sẽ tự sống trong 10 phút để giữ chỗ chờ VNPAY Webhook gọi tới
        }
    }
}
```

---

### 4.6. Xử Lý VNPAY HMAC-SHA512 An Toàn Tuyệt Đối Chống Timing Attack

#### File sắp xếp ASCII Alphabetical: `backend/src/WaterbusSystem.Infrastructure/Services/Payment/VnPayAsciiComparer.cs`
> **LƯU Ý QUAN TRỌNG**: File này chỉ có 1 nhiệm vụ duy nhất là sắp xếp các tham số URL theo bảng mã ASCII Alphabetical đúng quy định kỹ thuật của VNPAY (SortedList sorting), **KHÔNG DÙNG** để so sánh chữ ký hash!

```csharp
namespace WaterbusSystem.Infrastructure.Services.Payment;

/// <summary>
/// Sắp xếp tên tham số theo thứ tự mã ASCII Alphabetical theo đúng đặc tả của cổng VNPAY.
/// </summary>
public class VnPayAsciiComparer : IComparer<string>
{
    public int Compare(string? x, string? y) => string.CompareOrdinal(x, y);
}
```

#### File Service: `backend/src/WaterbusSystem.Infrastructure/Services/Payment/VnPayService.cs`
> **BẢO MẬT CHỐNG TIMING ATTACK**: Hàm `ValidateSignature` sử dụng `CryptographicOperations.FixedTimeEquals` để so sánh mảng byte băm trong thời gian cố định, loại trừ hoàn toàn nguy cơ rò rỉ thông tin qua kênh thời gian phản hồi.

```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services.Payment;

public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;
    private readonly SortedList<string, string> _requestData = new(new VnPayAsciiComparer());
    private readonly SortedList<string, string> _responseData = new(new VnPayAsciiComparer());

    public VnPayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreatePaymentUrl(string bookingCode, decimal amount, string orderInfo, string ipAddress)
    {
        var vnpayConfig = _configuration.GetSection("VnPay");
        var tmnCode = vnpayConfig["TmnCode"]!;
        var hashSecret = vnpayConfig["HashSecret"]!;
        var baseUrl = vnpayConfig["BaseUrl"]!;
        var returnUrl = vnpayConfig["ReturnUrl"]!;

        _requestData.Clear();
        _requestData.Add("vnp_Version", "2.1.0");
        _requestData.Add("vnp_Command", "pay");
        _requestData.Add("vnp_TmnCode", tmnCode);
        _requestData.Add("vnp_Amount", ((long)(amount * 100)).ToString());
        _requestData.Add("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
        _requestData.Add("vnp_CurrCode", "VND");
        _requestData.Add("vnp_IpAddr", ipAddress);
        _requestData.Add("vnp_Locale", "vn");
        _requestData.Add("vnp_OrderInfo", orderInfo);
        _requestData.Add("vnp_OrderType", "other");
        _requestData.Add("vnp_ReturnUrl", returnUrl);
        _requestData.Add("vnp_TxnRef", bookingCode);

        var data = new StringBuilder();
        foreach (var (key, value) in _requestData)
        {
            if (!string.IsNullOrEmpty(value))
            {
                data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
            }
        }

        var queryString = data.ToString().TrimEnd('&');
        var secureHash = HmacSha512(hashSecret, queryString);
        return $"{baseUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    /// <summary>
    /// Xác thực chữ ký số HMAC-SHA512 từ Webhook IPN / Callback của VNPAY.
    /// Sử dụng CryptographicOperations.FixedTimeEquals để phòng chống Timing Side-Channel Attack.
    /// </summary>
    public bool ValidateSignature(IQueryCollection query)
    {
        var hashSecret = _configuration["VnPay:HashSecret"]!;
        _responseData.Clear();

        string receivedSecureHash = string.Empty;
        foreach (var (key, value) in query)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
            {
                if (key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase))
                {
                    receivedSecureHash = value.ToString();
                }
                else
                {
                    _responseData.Add(key, value.ToString());
                }
            }
        }

        if (string.IsNullOrEmpty(receivedSecureHash))
        {
            return false;
        }

        var rawData = new StringBuilder();
        foreach (var (key, value) in _responseData)
        {
            if (!string.IsNullOrEmpty(value))
            {
                rawData.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
            }
        }

        var calculatedHash = HmacSha512(hashSecret, rawData.ToString().TrimEnd('&'));

        // SO SÁNH CONSTANT-TIME BẢO MẬT:
        var calculatedBytes = Encoding.UTF8.GetBytes(calculatedHash.ToLowerInvariant());
        var receivedBytes = Encoding.UTF8.GetBytes(receivedSecureHash.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(calculatedBytes, receivedBytes);
    }

    private static string HmacSha512(string key, string input)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(input);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLower();
    }
}
```

---

## 5. BỘ TIÊU CHÍ NGHIỆM THU CHO CODEX (DEFINITION OF DONE)

Codex hoàn thành setup khi thỏa mãn:
1. `dotnet build backend/WaterbusSystem.sln` chạy thành công không có lỗi biên dịch.
2. `dotnet test backend/tests/WaterbusSystem.UnitTests` vượt qua toàn bộ unit test:
   - Bài test xác minh `CryptographicOperations.FixedTimeEquals` và HMAC-SHA512 đúng chuẩn VNPAY.
   - Bài test xác minh `AcquireLockAsync` và `ReleaseLockAsync` với Redis Mock/Container.
3. `dotnet run --project backend/src/WaterbusSystem.WebApi` khởi động Web API tại cổng `http://localhost:5000`:
   - Mở được trang Swagger UI tại `http://localhost:5000/swagger`.
   - Có nút `Authorize` cho Bearer Token JWT.
   - Thử gọi `GET /api/v1/stations` trả về mã HTTP 200 kèm danh sách 5 bến tàu.
