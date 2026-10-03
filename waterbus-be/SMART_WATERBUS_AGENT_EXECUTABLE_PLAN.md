# SMART WATERBUS — Agent-Executable Implementation Plan
**Repository root:** `d:\FPT_Study\Semester_8\PRN232\smart-waterbus\waterbus-system\waterbus-be`  
**Source root:** `{repo}\src`  
**Domain project:** `{repo}\src\WaterbusSystem.Domain`  
**Application project:** `{repo}\src\WaterbusSystem.Application`  
**Infrastructure project:** `{repo}\src\WaterbusSystem.Infrastructure`  
**WebApi project:** `{repo}\src\WaterbusSystem.WebApi`  
**Tests project:** `{repo}\tests\WaterbusSystem.UnitTests`

---

## AGENT INSTRUCTIONS

- Thực hiện từng task theo đúng thứ tự ID (TASK-01 → TASK-45).
- Mỗi task có mục **"Done when"** — kiểm tra điều kiện đó trước khi chuyển sang task tiếp theo.
- Sau mỗi Phase hoàn tất: chạy `dotnet build` để đảm bảo không có compile error.
- Sau TASK-10: chạy `dotnet ef migrations add` để tạo migration.
- Không thay đổi gì ngoài phạm vi từng task.
- Nếu một task yêu cầu xóa code, phải đảm bảo không còn reference nào đến code đó.

---

# PHASE 1 — Hotfixes Bảo Mật & Data Integrity

---

## TASK-01 — Xóa Dispatcher & Accountant khỏi UserRole enum

**File:** `{repo}\src\WaterbusSystem.Domain\Enums\UserRole.cs`

**Xóa** 2 giá trị:
- `Dispatcher = 2`
- `Accountant = 3`

**Giữ lại** và **đánh số lại**:
```csharp
public enum UserRole
{
    Admin     = 1,
    Captain   = 2,
    Staff     = 3,
    Passenger = 4
}
```

**Done when:** File `UserRole.cs` chỉ còn 4 giá trị: Admin, Captain, Staff, Passenger. Build không lỗi.

---

## TASK-02 — Xóa Dispatcher & Accountant khỏi Seeder

**File:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\ApplicationDbContextInitializer.cs`

**Trong method `TrySeedAsync()`:**

Tìm dòng seed roles:
```csharp
var roles = new[] { "Admin", "Dispatcher", "Accountant", "Captain", "Staff", "Passenger" };
```
Đổi thành:
```csharp
var roles = new[] { "Admin", "Captain", "Staff", "Passenger" };
```

Xóa toàn bộ dòng gọi `SeedUserAsync` cho Dispatcher:
```csharp
// XÓA DÒNG NÀY:
await SeedUserAsync("dispatcher@waterbus.vn", "Điều Phối Viên Tuyến", "Dispatcher@123456!", "Dispatcher");
```

Giữ lại seed cho admin, staff. Thêm seed cho Captain:
```csharp
await SeedUserAsync("admin@waterbus.vn", "Admin Hệ Thống", "Admin@123456!", "Admin");
await SeedUserAsync("captain@waterbus.vn", "Thuyền Trưởng Mặc Định", "Captain@123456!", "Captain");
await SeedUserAsync("staff@waterbus.vn", "Nhân Viên Soát Vé Bến", "Staff@123456!", "Staff");
```

**Done when:** Seeder không còn tạo role/user Dispatcher và Accountant. Build không lỗi.

---

## TASK-03 — Sửa TripStatus enum

**File:** `{repo}\src\WaterbusSystem.Domain\Enums\TripStatus.cs`

**Thay toàn bộ nội dung enum:**
```csharp
public enum TripStatus
{
    Scheduled  = 1,  // Đã lên lịch, chưa bắt đầu
    Boarding   = 2,  // Đang đón khách tại bến đầu
    EnRoute    = 3,  // Tàu đang di chuyển
    Suspended  = 4,  // Tạm dừng (sự cố, thời tiết)
    Arrived    = 5,  // Tàu đã cập bến cuối
    Completed  = 6,  // Chuyến hoàn thành (nghiệp vụ đóng)
    Cancelled  = 7,  // Hủy chuyến
    Terminated = 8   // Buộc dừng giữa chừng
}
```

Giá trị `InTransit` và `Delayed` bị xóa.

**Sau đó:** Tìm toàn bộ project các chỗ reference `TripStatus.InTransit` và đổi thành `TripStatus.EnRoute`. Tìm `TripStatus.Delayed` và xóa/xử lý phù hợp.

**Done when:** Không còn reference nào đến `TripStatus.InTransit` hoặc `TripStatus.Delayed`. Build không lỗi.

---

## TASK-04 — Sửa Trip.BoatId thành nullable

**File 1:** `{repo}\src\WaterbusSystem.Domain\Entities\Trip.cs`

Tìm:
```csharp
public Guid BoatId { get; set; }
public Boat? Boat { get; set; }
```
Đổi thành:
```csharp
public Guid? BoatId { get; set; }
public Boat? Boat { get; set; }
```

**File 2:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `TripConfiguration.Configure()`, tìm đoạn config FK cho Boat:
```csharp
builder.HasOne(x => x.Boat)
    .WithMany()
    .HasForeignKey(x => x.BoatId)
    .OnDelete(DeleteBehavior.Restrict);
```
Thêm `.IsRequired(false)`:
```csharp
builder.HasOne(x => x.Boat)
    .WithMany()
    .HasForeignKey(x => x.BoatId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.Restrict);
```

**Done when:** `Trip.BoatId` là `Guid?`. Build không lỗi.

---

## TASK-05 — Thêm "Boat must be assigned before booking" validation

**File:** `{repo}\src\WaterbusSystem.Application\Features\Bookings\Commands\CreateBooking\CreateBookingCommand.cs`

Trong `CreateBookingCommandHandler.Handle()`, sau khi load trip (dòng `var trip = await _context.Trips...`), thêm validation:

```csharp
// Tàu phải được phân công trước khi mở bán
if (trip.BoatId == null)
{
    throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
    {
        new(nameof(request.TripId), "Chuyến tàu chưa được phân công tàu. Không thể đặt vé.")
    });
}
```

**Done when:** Handler throw ValidationException khi `trip.BoatId == null`. Build không lỗi.

---

## TASK-06 — Di chuyển việc tạo Ticket sang IPN handler (POST-payment only)

**File 1:** `{repo}\src\WaterbusSystem.Application\Features\Bookings\Commands\CreateBooking\CreateBookingCommand.cs`

Trong `CreateBookingCommandHandler.Handle()`, tìm và **XÓA** toàn bộ vòng lặp tạo Ticket:
```csharp
// XÓA ĐOẠN NÀY:
foreach (var seat in seats)
{
    booking.Tickets.Add(new Ticket
    {
        TripId = request.TripId,
        SeatId = seat.Id,
        TicketCode = CodeGenerator.GenerateTicketCode(now),
        Price = trip.BasePrice * seat.PriceMultiplier,
        PassengerName = request.CustomerName,
        Status = TicketStatus.Pending
    });

    _context.SeatReservations.Add(new SeatReservation { ... });
}
```

Thay bằng: chỉ tạo `SeatReservation`, không tạo `Ticket`:
```csharp
foreach (var seat in seats)
{
    _context.SeatReservations.Add(new SeatReservation
    {
        TripId = request.TripId,
        SeatId = seat.Id,
        BookingId = booking.Id,
        BoardingStopOrder = boardingStopOrder,
        DisembarkingStopOrder = disembarkingStopOrder,
        Status = ReservationStatus.Pending
    });
}

_context.Bookings.Add(booking);
```

**File 2:** `{repo}\src\WaterbusSystem.Application\Features\Payments\Commands\ConfirmVnPayIpn\ConfirmVnPayIpnCommand.cs`

Trong `ConfirmVnPayIpnCommandHandler.Handle()`, sau đoạn `if (isSuccess)`, thêm logic tạo Ticket:

```csharp
if (isSuccess)
{
    booking.Status = BookingStatus.Confirmed;
    booking.PaymentStatus = PaymentStatus.Success;

    // Load SeatReservations của Booking này
    var reservations = await _context.SeatReservations
        .Where(r => r.BookingId == booking.Id && r.Status == ReservationStatus.Pending)
        .Include(r => r.Seat)
        .ToListAsync(cancellationToken);

    foreach (var reservation in reservations)
    {
        reservation.Status = ReservationStatus.Confirmed;

        // Tạo Ticket SAU KHI xác nhận thanh toán thành công
        _context.Tickets.Add(new Ticket
        {
            BookingId = booking.Id,
            TripId = booking.TripId,  // NOTE: sẽ bị xóa ở Phase 2 khi refactor Ticket
            SeatId = reservation.SeatId,
            TicketCode = CodeGenerator.GenerateTicketCode(DateTimeOffset.UtcNow),
            Price = trip.BasePrice * (reservation.Seat?.PriceMultiplier ?? 1.0m),
            PassengerName = booking.CustomerName,
            Status = TicketStatus.Valid
        });
    }
}
else
{
    booking.Status = BookingStatus.Cancelled;
    booking.PaymentStatus = PaymentStatus.Failed;

    var reservations = await _context.SeatReservations
        .Where(r => r.BookingId == booking.Id && r.Status == ReservationStatus.Pending)
        .ToListAsync(cancellationToken);

    foreach (var reservation in reservations)
    {
        reservation.Status = ReservationStatus.Cancelled;
    }
    // Không tạo Ticket khi thanh toán thất bại
}
```

Cần thêm load `trip` trong handler này (hiện tại chưa load):
```csharp
var trip = await _context.Trips
    .FirstOrDefaultAsync(t => t.Id == booking.TripId && !t.IsDeleted, cancellationToken);
```

**File 3:** `{repo}\src\WaterbusSystem.Infrastructure\BackgroundJobs\ExpiredBookingCleanupService.cs`

Xóa toàn bộ đoạn xử lý Ticket trong cleanup (vì Ticket chưa tồn tại khi Booking còn Pending):
```csharp
// XÓA ĐOẠN NÀY:
foreach (var ticket in booking.Tickets.Where(t => t.Status == TicketStatus.Pending))
{
    ticket.Status = TicketStatus.Expired;
}
```

Xóa `.Include(b => b.Tickets)` khỏi query của cleanup job.

**Done when:** `CreateBooking` không còn tạo Ticket. `ConfirmVnPayIpn` tạo Ticket khi isSuccess=true. Build không lỗi.

---

## TASK-07 — Thêm IdempotencyKey vào PaymentTransaction

**File 1:** `{repo}\src\WaterbusSystem.Domain\Entities\PaymentTransaction.cs`

Thêm property:
```csharp
/// <summary>
/// Khóa chống xử lý trùng lặp IPN (Idempotency Key).
/// Giá trị = vnp_TxnRef + "_" + vnp_TransactionNo
/// </summary>
public string IdempotencyKey { get; set; } = string.Empty;
```

**File 2:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `PaymentTransactionConfiguration.Configure()`, thêm:
```csharp
builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
builder.HasIndex(x => x.IdempotencyKey).IsUnique();
```

**File 3:** `{repo}\src\WaterbusSystem.Application\Features\Payments\Commands\ConfirmVnPayIpn\ConfirmVnPayIpnCommand.cs`

Trong `Handle()`, TRƯỚC khi tạo PaymentTransaction, thêm check idempotency:
```csharp
var idempotencyKey = $"{bookingCode}_{vnpTransactionNo}";

var existingTransaction = await _context.PaymentTransactions
    .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);

if (existingTransaction != null)
{
    // Đã xử lý IPN này rồi, trả về thành công để VNPAY không retry nữa
    return new VnPayIpnResultDto("00", "Already processed");
}
```

Khi tạo `PaymentTransaction`, thêm `IdempotencyKey`:
```csharp
_context.PaymentTransactions.Add(new PaymentTransaction
{
    // ... existing fields ...
    IdempotencyKey = idempotencyKey
});
```

**Done when:** `PaymentTransaction` có column `IdempotencyKey` unique. IPN gọi 2 lần với cùng TransactionNo → lần 2 trả về `"00"/"Already processed"` mà không tạo thêm record. Build không lỗi.

---

## TASK-08 — Fix SeatReservation.BookingId thành non-nullable

**File 1:** `{repo}\src\WaterbusSystem.Domain\Entities\SeatReservation.cs`

Đổi:
```csharp
public Guid? BookingId { get; set; }
public Booking? Booking { get; set; }
```
Thành:
```csharp
public Guid BookingId { get; set; }
public Booking? Booking { get; set; }
```

**File 2:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `SeatReservationConfiguration.Configure()`, đổi:
```csharp
builder.HasOne(x => x.Booking)
    .WithMany()
    .HasForeignKey(x => x.BookingId)
    .OnDelete(DeleteBehavior.SetNull);
```
Thành:
```csharp
builder.HasOne(x => x.Booking)
    .WithMany(b => b.SeatReservations)
    .HasForeignKey(x => x.BookingId)
    .OnDelete(DeleteBehavior.Cascade);
```

**File 3:** `{repo}\src\WaterbusSystem.Domain\Entities\Booking.cs`

Thêm navigation collection (nếu chưa có):
```csharp
public ICollection<SeatReservation> SeatReservations { get; set; } = new List<SeatReservation>();
```

**File 4:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\ApplicationDbContext.cs`

Đảm bảo có `DbSet<SeatReservation>` (đã có, kiểm tra lại).

**Done when:** `SeatReservation.BookingId` là `Guid` non-nullable. FK có cascade delete. Build không lỗi.

---

## TASK-09 — Thêm Unique constraint cho Boat.CaptainUserId

**File:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `BoatConfiguration.Configure()`, thêm:
```csharp
// Unique partial index: 1 Captain chỉ được gán cho 1 Boat (nullable → dùng filter)
builder.HasIndex(x => x.CaptainUserId)
    .IsUnique()
    .HasFilter("[CaptainUserId] IS NOT NULL");
```

**Done when:** Unique index được tạo trên `CaptainUserId` (với filter nullable). Build không lỗi.

---

## TASK-10 — Thêm guest token expiry & revocation vào Booking

**File 1:** `{repo}\src\WaterbusSystem.Domain\Entities\Booking.cs`

Thêm 2 properties:
```csharp
/// <summary>
/// Thời điểm hết hạn của ManageOrderToken (NULL = chưa có token)
/// </summary>
public DateTimeOffset? ManageOrderTokenExpiresAt { get; set; }

/// <summary>
/// Token đã bị thu hồi (revoke) hay chưa
/// </summary>
public bool ManageOrderTokenRevoked { get; set; } = false;
```

**File 2:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `BookingConfiguration.Configure()`, thêm config cho 2 field mới:
```csharp
builder.Property(x => x.ManageOrderTokenExpiresAt);
builder.Property(x => x.ManageOrderTokenRevoked).IsRequired().HasDefaultValue(false);
```

**File 3:** `{repo}\src\WaterbusSystem.WebApi\Middlewares\GuestAccessMiddleware.cs`

Đổi query và validation trong `InvokeAsync()`:

Tìm:
```csharp
if (booking != null)
{
    context.Items[GuestOrderIdItemKey] = booking.Id;
    context.Items[GuestBookingIdItemKey] = booking.Id;
```

Đổi thành:
```csharp
if (booking != null
    && booking.ManageOrderTokenExpiresAt.HasValue
    && booking.ManageOrderTokenExpiresAt.Value > DateTimeOffset.UtcNow
    && !booking.ManageOrderTokenRevoked)
{
    context.Items[GuestOrderIdItemKey] = booking.Id;
    context.Items[GuestBookingIdItemKey] = booking.Id;
```

Cần select thêm các field mới trong query:
```csharp
var booking = await dbContext.Bookings
    .AsNoTracking()
    .Where(b => !b.IsDeleted && b.ManageOrderTokenHash == tokenHash)
    .Select(b => new
    {
        b.Id,
        b.ManageOrderTokenExpiresAt,
        b.ManageOrderTokenRevoked
    })
    .FirstOrDefaultAsync();
```

**Done when:** `Booking` có 2 field mới. Middleware kiểm tra expiry và revoked. Build không lỗi.

---

## TASK-11 — Thêm Rate Limiting cho BookingsController

**File 1:** `{repo}\src\WaterbusSystem.WebApi\Program.cs`

Thêm `using System.Threading.RateLimiting;` ở đầu file.

Trước `var app = builder.Build();`, thêm:
```csharp
// Rate Limiting: chống spam booking từ Guest/anonymous
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("BookingPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Quá nhiều yêu cầu đặt vé. Vui lòng thử lại sau 1 phút.",
            statusCode = 429
        }, token);
    };
});
```

Sau `app.UseAuthorization();`, thêm:
```csharp
app.UseRateLimiter();
```

**File 2:** `{repo}\src\WaterbusSystem.WebApi\Controllers\BookingsController.cs`

Thêm using:
```csharp
using Microsoft.AspNetCore.RateLimiting;
```

Thêm attribute vào action:
```csharp
[HttpPost]
[EnableRateLimiting("BookingPolicy")]
[ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public async Task<ActionResult<BookingResponseDto>> CreateBooking(...)
```

**Done when:** Rate limiting được đăng ký và gắn vào endpoint. Build không lỗi.

---

## TASK-12 — Tạo EF Core Migration Phase 1

**Chạy lệnh** từ thư mục `{repo}`:
```bash
dotnet ef migrations add Phase1_SecurityAndDataIntegrityFixes \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

**Sau khi tạo, review migration file** đảm bảo có các thay đổi:
- Column `IdempotencyKey` (varchar, not null, unique) trên bảng `PaymentTransactions`
- Column `ManageOrderTokenExpiresAt` (datetime2, nullable) trên bảng `Bookings`
- Column `ManageOrderTokenRevoked` (bit, not null, default 0) trên bảng `Bookings`
- `BookingId` trên `SeatReservations` đổi từ nullable thành NOT NULL
- FK `SeatReservation→Booking` cascade delete
- Unique index trên `Boats.CaptainUserId` (with filter IS NOT NULL)
- `BoatId` trên `Trips` đổi từ NOT NULL thành nullable

**Chạy để verify:**
```bash
dotnet ef database update \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

**Done when:** Migration tạo thành công, `dotnet run` ở WebApi project không throw exception khi startup.

---

# PHASE 2 — Mô Hình Thương Mại & Định Giá

---

## TASK-13 — Thêm entity SeatClass

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\SeatClass.cs`
```csharp
using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Hạng ghế (có thể cấu hình runtime, không phải enum cứng)
/// </summary>
public class SeatClass : BaseEntity
{
    public string Code { get; set; } = string.Empty;         // "SC01", "SC02", "SC03"
    public string Name { get; set; } = string.Empty;         // "Khoang trước VIP", "Tiêu chuẩn", "Boong ngoài"
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<FareRule> FareRules { get; set; } = new List<FareRule>();
}
```

**File:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Thêm class mới:
```csharp
public class SeatClassConfiguration : IEntityTypeConfiguration<SeatClass>
{
    public void Configure(EntityTypeBuilder<SeatClass> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

**File:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\ApplicationDbContext.cs`

Thêm DbSet:
```csharp
public DbSet<SeatClass> SeatClasses => Set<SeatClass>();
```

**File:** `{repo}\src\WaterbusSystem.Application\Common\Interfaces\IApplicationDbContext.cs`

Thêm:
```csharp
DbSet<SeatClass> SeatClasses { get; }
```

**Done when:** Entity `SeatClass` tồn tại, có EF config và DbSet. Build không lỗi.

---

## TASK-14 — Migrate Seat.Category enum → Seat.SeatClassId FK

**File 1:** `{repo}\src\WaterbusSystem.Domain\Entities\Seat.cs`

Xóa:
```csharp
public SeatCategory Category { get; set; } = SeatCategory.Standard;
public decimal PriceMultiplier { get; set; } = 1.0m;
```

Thêm:
```csharp
public Guid SeatClassId { get; set; }
public SeatClass? SeatClass { get; set; }
```

**File 2:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `SeatConfiguration.Configure()`:
- Xóa config của `Category` và `PriceMultiplier`
- Thêm:
```csharp
builder.HasOne(x => x.SeatClass)
    .WithMany(sc => sc.Seats)
    .HasForeignKey(x => x.SeatClassId)
    .OnDelete(DeleteBehavior.Restrict);
```

**File 3:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\ApplicationDbContextInitializer.cs`

Trong `TrySeedAsync()`, trước khi seed Boat/Seats, thêm seed SeatClass:
```csharp
// Seed SeatClass nếu chưa có
Guid frontCabinId, standardId, outdoorId;
if (!await _context.SeatClasses.AnyAsync())
{
    var seatClasses = new List<SeatClass>
    {
        new() { Code = "SC01", Name = "Khoang trước VIP", Description = "Tầm nhìn bao quát, điều hòa", IsActive = true },
        new() { Code = "SC02", Name = "Tiêu chuẩn", Description = "Khoang trong, máy lạnh", IsActive = true },
        new() { Code = "SC03", Name = "Boong ngoài trời", Description = "Phía đuôi tàu, thoáng mát", IsActive = true }
    };
    await _context.SeatClasses.AddRangeAsync(seatClasses);
    await _context.SaveChangesAsync();
    frontCabinId = seatClasses[0].Id;
    standardId   = seatClasses[1].Id;
    outdoorId    = seatClasses[2].Id;
}
else
{
    frontCabinId = (await _context.SeatClasses.FirstAsync(s => s.Code == "SC01")).Id;
    standardId   = (await _context.SeatClasses.FirstAsync(s => s.Code == "SC02")).Id;
    outdoorId    = (await _context.SeatClasses.FirstAsync(s => s.Code == "SC03")).Id;
}
```

Cập nhật đoạn tạo Seat trong seed Boat — đổi `Category =` thành `SeatClassId =`:
```csharp
// Khoang trước VIP: F01-F12
boat.Seats.Add(new Seat { SeatCode = $"F{i:D2}", SeatClassId = frontCabinId, ... });

// Standard: S01-S36
boat.Seats.Add(new Seat { SeatCode = $"S{i:D2}", SeatClassId = standardId, ... });

// Outdoor: O01-O12
boat.Seats.Add(new Seat { SeatCode = $"O{i:D2}", SeatClassId = outdoorId, ... });
```

**Done when:** `Seat` không còn `Category` hay `PriceMultiplier`, có `SeatClassId` FK. Build không lỗi.

---

## TASK-15 — Thêm entity FareRule

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\FareRule.cs`
```csharp
using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Bảng giá vé theo TripType × SeatClass.
/// Giá KHÔNG phụ thuộc Route, khoảng cách, giờ chạy hay ghế cụ thể.
/// EffectiveFrom/To chỉ dùng để quản lý phiên bản bảng giá, KHÔNG phải giá động theo thời gian.
/// </summary>
public class FareRule : BaseEntity
{
    public string TripType { get; set; } = string.Empty;         // "Commuter" hoặc "Sightseeing"
    public Guid SeatClassId { get; set; }
    public SeatClass? SeatClass { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByAdminId { get; set; }                  // FK → ApplicationUser (optional)
}
```

**EF Config** trong `EntityConfigurations.cs`:
```csharp
public class FareRuleConfiguration : IEntityTypeConfiguration<FareRule>
{
    public void Configure(EntityTypeBuilder<FareRule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TripType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Price).HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => new { x.TripType, x.SeatClassId, x.EffectiveFrom }).IsUnique();
        builder.HasOne(x => x.SeatClass)
            .WithMany(sc => sc.FareRules)
            .HasForeignKey(x => x.SeatClassId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

**DbSet** trong `ApplicationDbContext.cs` và `IApplicationDbContext.cs`:
```csharp
public DbSet<FareRule> FareRules => Set<FareRule>();
```

**Seed FareRule** trong `ApplicationDbContextInitializer.cs`:
```csharp
if (!await _context.FareRules.AnyAsync())
{
    var now = DateTimeOffset.UtcNow;
    var fareRules = new List<FareRule>
    {
        new() { TripType = "Commuter",    SeatClassId = frontCabinId, Price = 20000m, Currency = "VND", EffectiveFrom = now },
        new() { TripType = "Commuter",    SeatClassId = standardId,   Price = 15000m, Currency = "VND", EffectiveFrom = now },
        new() { TripType = "Commuter",    SeatClassId = outdoorId,    Price = 17000m, Currency = "VND", EffectiveFrom = now },
        new() { TripType = "Sightseeing", SeatClassId = frontCabinId, Price = 150000m, Currency = "VND", EffectiveFrom = now },
        new() { TripType = "Sightseeing", SeatClassId = standardId,   Price = 100000m, Currency = "VND", EffectiveFrom = now },
        new() { TripType = "Sightseeing", SeatClassId = outdoorId,    Price = 120000m, Currency = "VND", EffectiveFrom = now },
    };
    await _context.FareRules.AddRangeAsync(fareRules);
    await _context.SaveChangesAsync();
}
```

**Done when:** Entity `FareRule` tồn tại với unique index. Seed data đủ 6 records. Build không lỗi.

---

## TASK-16 — Xóa Trip.BasePrice & implement FareRule pricing

**File 1:** `{repo}\src\WaterbusSystem.Domain\Entities\Trip.cs`

Xóa:
```csharp
public decimal BasePrice { get; set; } = 15000m;
```

**File 2:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `TripConfiguration.Configure()`, xóa:
```csharp
builder.Property(x => x.BasePrice).HasPrecision(18, 2);
```

**File 3:** `{repo}\src\WaterbusSystem.Application\Features\Bookings\Commands\CreateBooking\CreateBookingCommand.cs`

Thay đổi cách tính giá khi tạo Booking.

Thêm logic lookup FareRule:
```csharp
// Tìm giá vé hiện hành theo TripType × SeatClass
var tripTypeStr = trip.TripType.ToString();
var now = DateTimeOffset.UtcNow;

// Load tất cả FareRule cần thiết một lần
var seatClassIds = seats.Select(s => s.SeatClassId).Distinct().ToList();
var fareRules = await _context.FareRules
    .Where(f => f.TripType == tripTypeStr
             && seatClassIds.Contains(f.SeatClassId)
             && f.IsActive
             && f.EffectiveFrom <= now
             && (f.EffectiveTo == null || f.EffectiveTo > now))
    .ToListAsync(cancellationToken);

// Helper để lấy giá theo SeatClassId
decimal GetFare(Guid seatClassId)
{
    var rule = fareRules
        .Where(f => f.SeatClassId == seatClassId)
        .OrderByDescending(f => f.EffectiveFrom)
        .FirstOrDefault()
        ?? throw new ValidationException(new List<FluentValidation.Results.ValidationFailure>
        {
            new("FareRule", $"Không tìm thấy bảng giá cho loại chuyến {tripTypeStr} và hạng ghế này.")
        });
    return rule.Price;
}
```

Đổi cách tính `TotalAmount`:
```csharp
var booking = new Booking
{
    ...
    TotalAmount = seats.Sum(s => GetFare(s.SeatClassId))
};
```

Trong vòng lặp tạo SeatReservation (đã remove Ticket ở TASK-06), không cần tính giá nữa ở đây (sẽ thêm `QuotedFare` ở Phase 2 sau).

**Done when:** `Trip.BasePrice` không còn tồn tại. Booking tính giá từ FareRule. Build không lỗi.

---

## TASK-17 — Thêm Route.ServiceType

**File 1:** `{repo}\src\WaterbusSystem.Domain\Entities\Route.cs`

Thêm property:
```csharp
/// <summary>
/// Loại dịch vụ: "Regular" (tuyến thường) hoặc "Sightseeing" (tuyến du lịch)
/// Phải khớp với TripType của các Trip thuộc Route này.
/// </summary>
public string ServiceType { get; set; } = "Regular";
```

**File 2:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `RouteConfiguration.Configure()`, thêm:
```csharp
builder.Property(x => x.ServiceType).HasMaxLength(20).IsRequired();
```

**File 3:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\ApplicationDbContextInitializer.cs`

Trong seed Route RT01, thêm `ServiceType = "Regular"`.

**Done when:** `Route.ServiceType` tồn tại, seeder set giá trị. Build không lỗi.

---

## TASK-18 — Thêm entity PurchaseOrder

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\PurchaseOrder.cs`
```csharp
using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Đơn hàng của người mua. 1 Order = 1 hoặc 2 Booking (OneWay/RoundTrip).
/// Người mua có thể là Passenger đăng nhập hoặc Guest (PassengerAccountId = null).
/// </summary>
public class PurchaseOrder : BaseEntity
{
    public Guid? PassengerAccountId { get; set; }     // NULL nếu Guest
    public string PurchaserName { get; set; } = string.Empty;
    public string PurchaserEmail { get; set; } = string.Empty;
    public string? PurchaserPhone { get; set; }
    public string PurchaseMode { get; set; } = "OneWay";   // "OneWay" hoặc "RoundTrip"
    public string Status { get; set; } = "Pending";         // "Pending", "Confirmed", "Cancelled"
    public decimal QuotedTotal { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
```

**EF Config:**
```csharp
public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PurchaserName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PurchaserEmail).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PurchaserPhone).HasMaxLength(20);
        builder.Property(x => x.PurchaseMode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.QuotedTotal).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

**DbSet** trong `ApplicationDbContext.cs` và `IApplicationDbContext.cs`:
```csharp
public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
```

**Done when:** Entity `PurchaseOrder` tồn tại. Build không lỗi.

---

## TASK-19 — Gắn Booking vào PurchaseOrder

**File:** `{repo}\src\WaterbusSystem.Domain\Entities\Booking.cs`

Thêm:
```csharp
public Guid OrderId { get; set; }
public PurchaseOrder? Order { get; set; }

// Public ID dùng trong QR (non-sensitive, có thể expose)
public string PublicBookingId { get; set; } = string.Empty;
public int QrCredentialVersion { get; set; } = 1;
public DateTimeOffset? QrIssuedAt { get; set; }
public DateTimeOffset? ConfirmedAt { get; set; }
public DateTimeOffset? CompletedAt { get; set; }
```

**File:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `BookingConfiguration.Configure()`, thêm:
```csharp
builder.HasIndex(x => x.PublicBookingId).IsUnique();
builder.Property(x => x.PublicBookingId).HasMaxLength(50).IsRequired();
builder.Property(x => x.QrCredentialVersion).IsRequired();

builder.HasOne(x => x.Order)
    .WithMany(o => o.Bookings)
    .HasForeignKey(x => x.OrderId)
    .OnDelete(DeleteBehavior.Restrict);
```

**Cập nhật `CreateBookingCommand.cs`:** Khi tạo Booking, phải tạo kèm PurchaseOrder:
```csharp
var order = new PurchaseOrder
{
    PurchaserName  = request.CustomerName,
    PurchaserEmail = request.CustomerEmail,
    PurchaserPhone = request.CustomerPhone,
    PurchaseMode   = "OneWay",
    Status         = "Pending",
    QuotedTotal    = seats.Sum(s => GetFare(s.SeatClassId)),
    ExpiresAt      = now.AddMinutes(10)
};

var booking = new Booking
{
    OrderId         = order.Id,
    BookingCode     = CodeGenerator.GenerateBookingCode(now),
    PublicBookingId = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
    Status          = BookingStatus.Pending,
    TotalAmount     = order.QuotedTotal
};

order.Bookings.Add(booking);
_context.PurchaseOrders.Add(order);
```

**Done when:** `Booking` có `OrderId` FK. Mỗi `CreateBooking` tạo cả `PurchaseOrder` lẫn `Booking`. Build không lỗi.

---

## TASK-20 — Migration Phase 2

```bash
dotnet ef migrations add Phase2_CommercialModelAndPricing \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

Verify migration có:
- Bảng `SeatClasses` mới
- Bảng `FareRules` mới
- Bảng `PurchaseOrders` mới
- Column `Seats.SeatClassId` (FK, not null) thay cho `Category` và `PriceMultiplier`
- Column `Bookings.OrderId`, `PublicBookingId`, `QrCredentialVersion`, `QrIssuedAt`, `ConfirmedAt`, `CompletedAt`
- `Trips.BasePrice` bị drop

**Done when:** Migration tạo thành công và `dotnet ef database update` chạy thành công.

---

# PHASE 3 — RouteStop & TripStopCall Infrastructure

---

## TASK-21 — Thêm entity RouteStop

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\RouteStop.cs`
```csharp
using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Danh sách bến dừng có thứ tự của một Tuyến đường.
/// SequenceNo: 1-indexed, theo chiều xuôi (chiều ngược dùng ScheduleStop.VisitOrder).
/// </summary>
public class RouteStop : BaseEntity
{
    public Guid RouteId { get; set; }
    public Route? Route { get; set; }
    public Guid StationId { get; set; }
    public Station? Station { get; set; }
    public int SequenceNo { get; set; }      // 1, 2, 3, ...
    public bool CanBoard { get; set; } = true;
    public bool CanAlight { get; set; } = true;

    public ICollection<ScheduleStop> ScheduleStops { get; set; } = new List<ScheduleStop>();
    public ICollection<TripStopCall> TripStopCalls { get; set; } = new List<TripStopCall>();
}
```

**EF Config:**
```csharp
public class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.RouteId, x.SequenceNo }).IsUnique();
        builder.HasOne(x => x.Route)
            .WithMany(r => r.Stops)
            .HasForeignKey(x => x.RouteId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Station)
            .WithMany()
            .HasForeignKey(x => x.StationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

**File `Route.cs`** — Thêm navigation:
```csharp
public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
```

**Done when:** `RouteStop` entity tồn tại. Build không lỗi.

---

## TASK-22 — Thêm entity ScheduleStop

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\ScheduleStop.cs`
```csharp
using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Định nghĩa thứ tự và thời gian offset của từng bến trong một Schedule.
/// VisitOrder hỗ trợ chiều ngược (Inbound) và vòng lặp (loop routes).
/// </summary>
public class ScheduleStop : BaseEntity
{
    public Guid ScheduleId { get; set; }
    public Schedule? Schedule { get; set; }
    public Guid RouteStopId { get; set; }
    public RouteStop? RouteStop { get; set; }
    public int VisitOrder { get; set; }              // Thứ tự ghé trong chuyến
    public int? ArrivalOffsetMin { get; set; }       // Phút từ giờ khởi hành
    public int? DepartureOffsetMin { get; set; }
}
```

**EF Config:**
```csharp
public class ScheduleStopConfiguration : IEntityTypeConfiguration<ScheduleStop>
{
    public void Configure(EntityTypeBuilder<ScheduleStop> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ScheduleId, x.VisitOrder }).IsUnique();
        builder.HasOne(x => x.Schedule)
            .WithMany(s => s.Stops)
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RouteStop)
            .WithMany(rs => rs.ScheduleStops)
            .HasForeignKey(x => x.RouteStopId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

**File `Schedule.cs`** — Thêm:
```csharp
public string Direction { get; set; } = "Outbound";         // "Outbound" hoặc "Inbound"
public DateOnly EffectiveFrom { get; set; }
public DateOnly? EffectiveTo { get; set; }
public Guid? CreatedByAdminId { get; set; }
public ICollection<ScheduleStop> Stops { get; set; } = new List<ScheduleStop>();
```

**Done when:** Entities `ScheduleStop` tồn tại. `Schedule` có `Direction`, `EffectiveFrom`, `EffectiveTo`. Build không lỗi.

---

## TASK-23 — Thêm entity TripStopCall

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\TripStopCall.cs`
```csharp
using WaterbusSystem.Domain.Common;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Bến dừng thực tế của một Trip cụ thể.
/// Mỗi Trip có danh sách TripStopCalls, mỗi call là một lần ghé bến.
/// </summary>
public class TripStopCall : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid RouteStopId { get; set; }
    public RouteStop? RouteStop { get; set; }
    public Guid? ScheduleStopId { get; set; }         // NULL nếu ad-hoc
    public ScheduleStop? ScheduleStop { get; set; }
    public int VisitOrder { get; set; }                // Thứ tự ghé trong trip

    public DateTimeOffset? PlannedArrivalAt { get; set; }
    public DateTimeOffset? PlannedDepartureAt { get; set; }
    public DateTimeOffset? ActualArrivalAt { get; set; }
    public DateTimeOffset? ActualDepartureAt { get; set; }
    public DateTimeOffset? CheckinOpenAt { get; set; }
    public DateTimeOffset? CheckinCloseAt { get; set; }

    /// <summary>
    /// "Planned", "Arrived", "Departed", "Skipped"
    /// </summary>
    public string StopStatus { get; set; } = "Planned";
}
```

**EF Config:**
```csharp
public class TripStopCallConfiguration : IEntityTypeConfiguration<TripStopCall>
{
    public void Configure(EntityTypeBuilder<TripStopCall> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TripId, x.VisitOrder }).IsUnique();
        builder.Property(x => x.StopStatus).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.Trip)
            .WithMany(t => t.StopCalls)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RouteStop)
            .WithMany(rs => rs.TripStopCalls)
            .HasForeignKey(x => x.RouteStopId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScheduleStop)
            .WithMany()
            .HasForeignKey(x => x.ScheduleStopId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

**File `Trip.cs`** — Thêm navigation:
```csharp
public ICollection<TripStopCall> StopCalls { get; set; } = new List<TripStopCall>();
```

**DbSets** trong `ApplicationDbContext.cs` và `IApplicationDbContext.cs`:
```csharp
public DbSet<RouteStop> RouteStops => Set<RouteStop>();
public DbSet<ScheduleStop> ScheduleStops => Set<ScheduleStop>();
public DbSet<TripStopCall> TripStopCalls => Set<TripStopCall>();
```

**Done when:** 3 entities mới có EF config và DbSet. Build không lỗi.

---

## TASK-24 — Refactor Booking để dùng TripStopCall

**File:** `{repo}\src\WaterbusSystem.Domain\Entities\Booking.cs`

Thêm:
```csharp
public Guid TripId { get; set; }
public Trip? Trip { get; set; }
public Guid BoardingCallId { get; set; }
public TripStopCall? BoardingCall { get; set; }
public Guid DisembarkingCallId { get; set; }
public TripStopCall? DisembarkingCall { get; set; }
```

**File:** `{repo}\src\WaterbusSystem.Infrastructure\Persistence\Configurations\EntityConfigurations.cs`

Trong `BookingConfiguration.Configure()`, thêm:
```csharp
builder.HasOne(x => x.Trip)
    .WithMany()
    .HasForeignKey(x => x.TripId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.BoardingCall)
    .WithMany()
    .HasForeignKey(x => x.BoardingCallId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.DisembarkingCall)
    .WithMany()
    .HasForeignKey(x => x.DisembarkingCallId)
    .OnDelete(DeleteBehavior.Restrict);
```

**File:** `{repo}\src\WaterbusSystem.Application\Features\Bookings\Commands\CreateBooking\CreateBookingCommand.cs`

**Thay đổi command input:**
```csharp
public record CreateBookingCommand(
    Guid TripId,
    List<Guid> SeatIds,
    Guid BoardingCallId,           // đổi từ BoardingStationId
    Guid DisembarkingCallId,       // đổi từ DisembarkingStationId
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone) : IRequest<BookingResponseDto>;
```

**Thay đổi handler** — Xóa toàn bộ logic tính `boardingStopOrder/disembarkingStopOrder` dùng `Station.OrderIndex`. Thay bằng:
```csharp
// Load TripStopCalls
var boardingCall = await _context.TripStopCalls
    .Include(c => c.RouteStop)
    .FirstOrDefaultAsync(c => c.Id == request.BoardingCallId && c.TripId == request.TripId, cancellationToken)
    ?? throw new NotFoundException(nameof(TripStopCall), request.BoardingCallId);

var disembarkingCall = await _context.TripStopCalls
    .Include(c => c.RouteStop)
    .FirstOrDefaultAsync(c => c.Id == request.DisembarkingCallId && c.TripId == request.TripId, cancellationToken)
    ?? throw new NotFoundException(nameof(TripStopCall), request.DisembarkingCallId);

// Validate thứ tự
if (boardingCall.VisitOrder >= disembarkingCall.VisitOrder)
    throw new ValidationException(...);

// Validate CanBoard / CanAlight
if (!boardingCall.RouteStop!.CanBoard)
    throw new ValidationException(...);
if (!disembarkingCall.RouteStop!.CanAlight)
    throw new ValidationException(...);
```

**Cập nhật SeatAvailabilityService** để so sánh `VisitOrder` thay vì `BoardingStopOrder/DisembarkingStopOrder`:
- Xóa `BoardingStopOrder`, `DisembarkingStopOrder` khỏi `SeatReservation`
- Thay bằng query join qua `Booking.BoardingCall.VisitOrder`

**File:** `{repo}\src\WaterbusSystem.Domain\Entities\SeatReservation.cs`

Xóa:
```csharp
public int BoardingStopOrder { get; set; }
public int DisembarkingStopOrder { get; set; }
```

**Done when:** `CreateBooking` nhận `BoardingCallId`/`DisembarkingCallId`. Overlap check dùng `VisitOrder`. Build không lỗi.

---

## TASK-25 — Migration Phase 3

```bash
dotnet ef migrations add Phase3_RouteStopAndTripStopCallInfrastructure \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

Verify migration có:
- Bảng `RouteStops`, `ScheduleStops`, `TripStopCalls`
- Columns mới trên `Bookings`: `TripId`, `BoardingCallId`, `DisembarkingCallId`
- `Schedules`: `Direction`, `EffectiveFrom`, `EffectiveTo`, `CreatedByAdminId`
- `SeatReservations`: drop `BoardingStopOrder`, `DisembarkingStopOrder`

**Done when:** Migration và database update thành công.

---

# PHASE 4 — Schedule → Trip Generation & Trip Management

---

## TASK-26 — Admin command: Assign Boat to Trip

**Tạo file mới:** `{repo}\src\WaterbusSystem.Application\Features\Trips\Commands\AssignBoat\AssignBoatToTripCommand.cs`

```csharp
public record AssignBoatToTripCommand(Guid TripId, Guid BoatId) : IRequest<Unit>;

public class AssignBoatToTripCommandHandler : IRequestHandler<AssignBoatToTripCommand, Unit>
{
    // Handler logic:
    // 1. Load Trip (phải tồn tại, không IsDeleted)
    // 2. Load Boat (phải IsActive, có CaptainUserId != null)
    // 3. Kiểm tra Boat không bị overlap về thời gian với Trip khác:
    //    Tìm Trip khác có BoatId = request.BoatId
    //    AND Status NOT IN (Cancelled, Terminated, Completed)
    //    AND PlannedDepartureAt range overlap với Trip hiện tại
    //    Nếu có → throw ValidationException("Tàu đã được phân công cho chuyến khác trong khoảng thời gian này.")
    // 4. Trip.BoatId = request.BoatId
    // 5. SaveChanges
}
```

**Controller mới:** `{repo}\src\WaterbusSystem.WebApi\Controllers\TripsController.cs`

Thêm endpoint:
```csharp
[HttpPut("{tripId}/assign-boat")]
[Authorize(Policy = "AdminOnly")]
public async Task<IActionResult> AssignBoat(Guid tripId, [FromBody] Guid boatId)
```

**Done when:** Endpoint tồn tại, validate boat overlap, assign thành công. Build không lỗi.

---

## TASK-27 — Admin command: Generate Trips from Schedule

**Tạo file mới:** `{repo}\src\WaterbusSystem.Application\Features\Trips\Commands\GenerateTrips\GenerateTripsFromScheduleCommand.cs`

```csharp
public record GenerateTripsFromScheduleCommand(
    Guid ScheduleId,
    DateOnly FromDate,
    DateOnly ToDate) : IRequest<int>; // Trả về số Trip được tạo

public class GenerateTripsFromScheduleCommandHandler : IRequestHandler<...>
{
    // Handler logic:
    // 1. Load Schedule + ScheduleStops (include RouteStop.Station)
    // 2. Validate Schedule.IsActive = true
    // 3. Validate FromDate <= ToDate, ToDate <= Schedule.EffectiveTo (nếu có)
    // 4. Parse DaysOfWeek pattern
    // 5. Loop qua từng ngày trong [FromDate, ToDate]:
    //    a. Check ngày có trong DaysOfWeek không
    //    b. Tính PlannedDepartureAt = date + Schedule.DepartureTime
    //    c. Check chưa có Trip với (ScheduleId, PlannedDepartureAt) (unique constraint)
    //    d. Tạo Trip (Status=Scheduled, BoatId=null, ScheduleId=scheduleId, RouteId=schedule.RouteId)
    //    e. Tạo TripStopCalls từ ScheduleStops:
    //       - VisitOrder = scheduleStop.VisitOrder
    //       - PlannedArrivalAt = PlannedDepartureAt + ArrivalOffsetMin
    //       - PlannedDepartureAt = PlannedDepartureAt + DepartureOffsetMin
    //       - CheckinOpenAt = PlannedArrivalAt - 30 minutes
    //       - CheckinCloseAt = PlannedDepartureAt
    //       - StopStatus = "Planned"
    // 6. SaveChanges (batch)
    // 7. Return count of created trips
}
```

**Done when:** Command tạo Trips + TripStopCalls đúng từ Schedule. Build không lỗi.

---

# PHASE 5 — Thanh Toán & Hoàn Tiền

---

## TASK-28 — Thêm entity FinancialTransaction (thay PaymentTransaction)

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\FinancialTransaction.cs`
```csharp
public class FinancialTransaction : BaseEntity
{
    public Guid OrderId { get; set; }
    public PurchaseOrder? Order { get; set; }
    public Guid? RefundBookingId { get; set; }          // bắt buộc nếu Type = Refund
    public Booking? RefundBooking { get; set; }
    public Guid? OriginalPaymentId { get; set; }        // bắt buộc nếu Type = Refund
    public FinancialTransaction? OriginalPayment { get; set; }

    public string TransactionType { get; set; } = "Payment";  // "Payment" hoặc "Refund"
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "Requested";   // "Requested","Processing","Succeeded","Failed"
    public string? GatewayName { get; set; }             // "VNPAY"
    public string? GatewayReference { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;  // UNIQUE
    public string? FailureReason { get; set; }
    public string? ManualRefundEvidence { get; set; }
    public Guid? ManualProcessedByAdminId { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<RefundTicketAllocation> RefundAllocations { get; set; } = new List<RefundTicketAllocation>();
}
```

**EF Config, DbSet, IApplicationDbContext:** tương tự các entity trên.

**Migrate `ConfirmVnPayIpnCommand`** để tạo `FinancialTransaction` thay vì `PaymentTransaction`.

**Cập nhật `IApplicationDbContext`** — thêm `DbSet<FinancialTransaction>`. Giữ `DbSet<PaymentTransaction>` cho đến khi có migration xóa bảng cũ.

**Done when:** `FinancialTransaction` entity tồn tại. IPN handler dùng entity mới. Build không lỗi.

---

## TASK-29 — Thêm entity RefundTicketAllocation

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\RefundTicketAllocation.cs`
```csharp
public class RefundTicketAllocation : BaseEntity
{
    public Guid RefundTransactionId { get; set; }
    public FinancialTransaction? RefundTransaction { get; set; }
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public decimal Amount { get; set; }
}
```

**EF Config:** unique index `(RefundTransactionId, TicketId)`.

**Done when:** Entity tồn tại với unique constraint. Build không lỗi.

---

## TASK-30 — Implement Refund Flow: Trip Cancelled

**Tạo file mới:** `{repo}\src\WaterbusSystem.Application\Features\Trips\Commands\CancelTrip\CancelTripCommand.cs`

```csharp
public record CancelTripCommand(Guid TripId, string Reason) : IRequest<Unit>;

// Handler logic:
// 1. Load Trip (phải Scheduled hoặc Boarding)
// 2. Trip.Status = Cancelled
// 3. Load tất cả Booking active (Confirmed) của Trip này
// 4. Với mỗi Booking:
//    a. Load Tickets có Status = Valid hoặc CheckedIn
//    b. Tạo FinancialTransaction(type=Refund):
//       - OrderId = booking.OrderId
//       - RefundBookingId = booking.Id
//       - OriginalPaymentId = payment thành công của order này
//       - Amount = sum(ticket.PaidFareSnapshot) cho các ticket đủ điều kiện
//       - Status = "Requested"
//       - IdempotencyKey = "refund_cancel_{tripId}_{bookingId}"
//    c. Tạo RefundTicketAllocation cho mỗi Ticket: amount = ticket.PaidFareSnapshot
//    d. Booking.Status = Cancelled
//    e. Ticket.Status = Cancelled
// 5. Tạo TripOperationEvent (action=Cancelled, actedByAdminId=current user)
// 6. SaveChanges
```

**Duplicate refund prevention:** Trước bước 4b, check:
```csharp
var existingRefund = await _context.FinancialTransactions
    .AnyAsync(t => t.IdempotencyKey == $"refund_cancel_{tripId}_{bookingId}");
if (existingRefund) continue; // Skip, đã refund rồi
```

**Done when:** Command tồn tại, logic đúng BR. Build không lỗi.

---

## TASK-31 — Migration Phase 5

```bash
dotnet ef migrations add Phase5_FinancialTransactionAndRefund \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

**Done when:** Migration và database update thành công.

---

# PHASE 6 — QR & Check-in

---

## TASK-32 — Redesign QR Model (per-Booking, digitally signed)

**Xóa** `Ticket.QrSeed` khỏi `Ticket.cs`.

**Thêm interface** `{repo}\src\WaterbusSystem.Application\Common\Interfaces\IBookingQrService.cs`:
```csharp
public interface IBookingQrService
{
    string GenerateQrPayload(Guid bookingId, string publicBookingId, int version);
    bool VerifyQrPayload(string payload, out string publicBookingId);
}
```

**Implement** `{repo}\src\WaterbusSystem.Infrastructure\Services\BookingQrService.cs`:
```csharp
// QR payload = Base64(JSON({ bid, v, iat, sig }))
// sig = HMAC-SHA256(bid + "_" + v + "_" + iat, QR_SECRET)
```

**Endpoint mới** trong `BookingsController`:
```csharp
[HttpGet("{id}/qr")]
[AllowAnonymous] // Guest cần xem QR sau khi book
public async Task<IActionResult> GetBookingQr(Guid id)
// → Trả về QR payload hoặc PNG image
// → Verify booking tồn tại, đã Confirmed
// → Generate và cache QrPayload vào Booking.QrPayload
```

**Done when:** QR là per-Booking, có chữ ký. Ticket không còn QrSeed. Build không lỗi.

---

## TASK-33 — Thêm ScannerDevice, ScannerAssignment, CheckInEvent entities

**Tạo 3 file** trong `{repo}\src\WaterbusSystem.Domain\Entities\`:

**`ScannerDevice.cs`:**
```csharp
public class ScannerDevice : BaseEntity
{
    public string DeviceCode { get; set; } = string.Empty;  // unique
    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastPreloadedAt { get; set; }
}
```

**`ScannerAssignment.cs`:**
```csharp
public class ScannerAssignment : BaseEntity
{
    public Guid DeviceId { get; set; }
    public ScannerDevice? Device { get; set; }
    public Guid StaffAccountId { get; set; }
    public Guid TripStopCallId { get; set; }
    public TripStopCall? TripStopCall { get; set; }
    public bool IsPrimaryOffline { get; set; }
    public DateTimeOffset ActiveFrom { get; set; }
    public DateTimeOffset? ActiveUntil { get; set; }
}
```

**`CheckInEvent.cs`:**
```csharp
public class CheckInEvent : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public Guid StaffAccountId { get; set; }
    public Guid? ScannerDeviceId { get; set; }
    public ScannerDevice? ScannerDevice { get; set; }
    public Guid TripStopCallId { get; set; }
    public TripStopCall? TripStopCall { get; set; }
    public string ClientEventId { get; set; } = string.Empty;  // UNIQUE - dedup key
    public DateTimeOffset OccurredAtDevice { get; set; }
    public DateTimeOffset? ReceivedAtServer { get; set; }
    public string Outcome { get; set; } = string.Empty;   // "Success","AlreadyCheckedIn","InvalidStop",...
    public string SyncStatus { get; set; } = "Synced";   // "Synced","Pending","Conflict"
    public string? Notes { get; set; }
}
```

**EF Configs:** unique index `CheckInEvent.ClientEventId`. Unique partial index trên `ScannerAssignment`: không cho 2 primary offline cùng TripStopCallId cùng lúc (enforce ở application layer).

**Done when:** 3 entities tồn tại với EF config. Build không lỗi.

---

## TASK-34 — Implement Check-in API

**Tạo controller mới:** `{repo}\src\WaterbusSystem.WebApi\Controllers\CheckInController.cs`

**Endpoint 1 — Online check-in:**
```csharp
[HttpPost("scan")]
[Authorize(Policy = "StaffOnly")]
public async Task<IActionResult> Scan([FromBody] ScanBookingCommand command)
// Input: BookingPublicId (từ QR), TripStopCallId, TicketIds[]
// Logic:
//   1. Verify QR payload (IBookingQrService.VerifyQrPayload)
//   2. Load Booking theo PublicBookingId
//   3. Validate TripStopCallId thuộc Booking.TripId
//   4. Validate CheckinOpenAt <= now (hoặc Trip.Status = EnRoute)
//   5. Với mỗi TicketId:
//      a. Ticket phải thuộc Booking này
//      b. Ticket.BoardingStatus phải là "NotBoarded"
//      c. Tạo CheckInEvent(Outcome=Success)
//      d. Ticket.BoardingStatus = "CheckedIn"
//      e. Ticket.CheckedInAt = now
```

**Endpoint 2 — Offline sync:**
```csharp
[HttpPost("sync")]
[Authorize(Policy = "StaffOnly")]
public async Task<IActionResult> Sync([FromBody] List<CheckInEventDto> events)
// Logic: Upsert theo ClientEventId (idempotent)
//   Với mỗi event:
//     - Nếu CheckInEvent.ClientEventId đã tồn tại → skip
//     - Nếu chưa → xử lý như online check-in, set SyncStatus = "Synced"
//     - SortBy OccurredAtDevice để xử lý đúng thứ tự
```

**Done when:** Cả 2 endpoints tồn tại và hoạt động đúng. Build không lỗi.

---

## TASK-35 — Migration Phase 6

```bash
dotnet ef migrations add Phase6_QrAndCheckIn \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

**Done when:** Migration và database update thành công.

---

# PHASE 7 — Incident & Vận Hành

---

## TASK-36 — Thêm Incident, IncidentTrip, TripOperationEvent entities

**Tạo 3 file** trong `{repo}\src\WaterbusSystem.Domain\Entities\`:

**`Incident.cs`:**
```csharp
public class Incident : BaseEntity
{
    public string ScopeType { get; set; } = string.Empty;   // "Route","Boat","Trip","MultipleTrips"
    public Guid? RouteId { get; set; }
    public Guid? BoatId { get; set; }
    public Guid? ReportedByStaffId { get; set; }
    public Guid? ReportedByCaptainId { get; set; }
    public Guid? HandledByAdminId { get; set; }
    public string Source { get; set; } = string.Empty;      // "Staff","Captain","System","WeatherService"
    public string IncidentType { get; set; } = string.Empty; // "Technical","Safety","Weather","Other"
    public string? Severity { get; set; }
    public string Status { get; set; } = "Open";            // "Open","Resolved","Closed"
    public string? Description { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public ICollection<IncidentTrip> AffectedTrips { get; set; } = new List<IncidentTrip>();
}
```

**`IncidentTrip.cs`:**
```csharp
public class IncidentTrip : BaseEntity
{
    public Guid IncidentId { get; set; }
    public Incident? Incident { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid? DecisionByAdminId { get; set; }
    public string? OperationDecision { get; set; }    // "Suspend","Cancel","None"
    public DateTimeOffset? DecisionAt { get; set; }
    // UNIQUE: (IncidentId, TripId)
}
```

**`TripOperationEvent.cs`:**
```csharp
public class TripOperationEvent : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid? IncidentId { get; set; }
    public Incident? Incident { get; set; }
    public Guid? ActedByAdminId { get; set; }
    public Guid? ActedByCaptainId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string? Note { get; set; }
}
```

**Business rule phải enforce ở handler:** Incident KHÔNG tự động thay đổi `Trip.Status`. Admin phải ra quyết định rõ ràng qua `IncidentTrip.OperationDecision` → thực thi qua `TripOperationEvent`.

**Done when:** 3 entities tồn tại với EF config và DbSet. Build không lỗi.

---

## TASK-37 — Incident API endpoints

**Tạo controller mới:** `{repo}\src\WaterbusSystem.WebApi\Controllers\IncidentsController.cs`

```csharp
[HttpPost]                          // Captain/Staff/System báo cáo sự cố
[HttpGet("{id}")]                   // Admin xem chi tiết
[HttpPost("{id}/trips")]            // Admin gắn Trip vào Incident
[HttpPost("{id}/trips/{tripId}/decision")]  // Admin ra quyết định per-Trip
```

**Tạo command mới:** `ChangeTripStatusCommand`
- Chỉ Admin hoặc Captain được thực thi (tuỳ action)
- Validate state machine hợp lệ (ví dụ: không Cancel khi đã Completed)
- Tạo `TripOperationEvent` mỗi khi status thay đổi

**Done when:** Endpoints tồn tại. Status change luôn tạo `TripOperationEvent`. Build không lỗi.

---

## TASK-38 — Migration Phase 7

```bash
dotnet ef migrations add Phase7_IncidentAndOperations \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

**Done when:** Migration và database update thành công.

---

# PHASE 8 — Identity & Access Profiles

---

## TASK-39 — Thêm profile entities (PassengerProfile, AdminProfile, StaffProfile, CaptainProfile)

**Tạo 4 file** trong `{repo}\src\WaterbusSystem.Domain\Entities\`:

```csharp
// PassengerProfile.cs
public class PassengerProfile : BaseEntity
{
    public Guid AccountId { get; set; }  // PK = FK → ApplicationUser.Id
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

// AdminProfile.cs
public class AdminProfile : BaseEntity
{
    public Guid AccountId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;  // unique
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

// StaffProfile.cs
public class StaffProfile : BaseEntity
{
    public Guid AccountId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;  // unique
    public string FullName { get; set; } = string.Empty;
    public Guid? HomeStationId { get; set; }
    public Station? HomeStation { get; set; }
    public bool IsActive { get; set; } = true;
}

// CaptainProfile.cs
public class CaptainProfile : BaseEntity
{
    public Guid AccountId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;  // unique
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
```

**EF Config:** AccountId là cả PK và FK (table-per-type pattern). Unique index trên `EmployeeCode` cho Admin, Staff, Captain.

**Done when:** 4 profile entities tồn tại. Build không lỗi.

---

## TASK-40 — Thêm entity AccessGrant

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\AccessGrant.cs`
```csharp
public class AccessGrant : BaseEntity
{
    public Guid OrderId { get; set; }
    public PurchaseOrder? Order { get; set; }
    public Guid? BookingId { get; set; }       // chỉ cho PassengerTrip scope
    public Booking? Booking { get; set; }
    public string AccessType { get; set; } = string.Empty;  // "ManageOrder","PassengerTrip"
    public string TokenHash { get; set; } = string.Empty;   // SHA-256 hash, UNIQUE
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
```

**EF Config:** unique index `TokenHash`.

**Xóa** khỏi `Booking.cs`:
- `ManageOrderTokenHash`
- `ManageOrderTokenExpiresAt`
- `ManageOrderTokenRevoked`
(Đã thêm tạm ở TASK-10 — bây giờ chuyển sang `AccessGrant`)

**Cập nhật `GuestAccessMiddleware.cs`** để query `AccessGrant` thay vì `Booking`:
```csharp
var grant = await dbContext.AccessGrants
    .Where(g => g.TokenHash == tokenHash
             && g.RevokedAt == null
             && g.ExpiresAt > DateTimeOffset.UtcNow)
    .Select(g => new { g.OrderId, g.BookingId, g.AccessType })
    .FirstOrDefaultAsync();

if (grant != null)
{
    context.Items["GuestOrderId"] = grant.OrderId;
    context.Items["GuestBookingId"] = grant.BookingId;
    context.Items["GuestAccessType"] = grant.AccessType;
    // Cập nhật LastUsedAt
}
```

**Done when:** `AccessGrant` entity tồn tại. GuestMiddleware dùng entity mới. Build không lỗi.

---

## TASK-41 — Migration Phase 8

```bash
dotnet ef migrations add Phase8_IdentityProfilesAndAccessGrant \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

**Done when:** Migration và database update thành công.

---

# PHASE 9 — Sightseeing & GPS

---

## TASK-42 — Thêm PointOfInterest, RoutePOI, AudioGuide entities

**Tạo 3 file** trong `{repo}\src\WaterbusSystem.Domain\Entities\`:

```csharp
// PointOfInterest.cs
public class PointOfInterest : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
}

// RoutePOI.cs
public class RoutePOI : BaseEntity
{
    public Guid RouteId { get; set; }
    public Route? Route { get; set; }
    public Guid PoiId { get; set; }
    public PointOfInterest? Poi { get; set; }
    public int DisplayOrder { get; set; }
    public int? TriggerRadiusM { get; set; }
    public bool IsActive { get; set; } = true;
    // Unique: (RouteId, DisplayOrder)
}

// AudioGuide.cs
public class AudioGuide : BaseEntity
{
    public Guid PoiId { get; set; }
    public PointOfInterest? Poi { get; set; }
    public string LanguageCode { get; set; } = "vi";    // Chỉ "vi" trong scope hiện tại
    public string AudioAssetUri { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public bool IsPublished { get; set; } = false;
    public DateTimeOffset UpdatedAt { get; set; }
    // Unique: (PoiId, LanguageCode)
}
```

---

## TASK-43 — Thêm BoatPositionEvent entity & GPS endpoint

**Tạo file mới:** `{repo}\src\WaterbusSystem.Domain\Entities\BoatPositionEvent.cs`
```csharp
public class BoatPositionEvent : BaseEntity
{
    public Guid BoatId { get; set; }
    public Boat? Boat { get; set; }
    public Guid? TripId { get; set; }
    public Trip? Trip { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public string Source { get; set; } = "GPS";    // "GPS" hoặc "Simulated"
    public decimal? SpeedKnots { get; set; }
}
```

**Endpoint mới:** `POST /api/boats/{id}/position` (Captain role)
```csharp
// Input: Latitude, Longitude, ObservedAt, SpeedKnots?
// Logic:
//   1. Tạo BoatPositionEvent
//   2. Tìm TripId active của Boat này (Trip.Status = EnRoute AND Trip.BoatId = boatId)
//   3. Nếu có Trip và Trip thuộc Sightseeing Route:
//      Load RoutePOIs của Route, check proximity với TriggerRadiusM
//      Nếu trong radius → publish event (log hoặc SignalR) để onboard player phát audio
//   4. Business rule: Ticket.BoardingStatus KHÔNG ảnh hưởng trigger
```

**Done when:** Entity và endpoint tồn tại. Build không lỗi.

---

## TASK-44 — Migration Phase 9

```bash
dotnet ef migrations add Phase9_SightseeingAndGps \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

**Done when:** Migration và database update thành công.

---

# PHASE 10 — Cleanup & Tests

---

## TASK-45 — Cleanup, Performance & Tests

**Xóa legacy items:**
- `SeatCategory.cs` — Xóa file hoàn toàn (đã thay bằng SeatClass entity ở TASK-13)
- `SeatStatus.cs` — Xóa file (không dùng trong segment-based model)
- `PaymentTransaction.cs` — Deprecated, drop bảng trong migration (đã thay bằng FinancialTransaction)
- Xóa `Boat.TotalSeats`, `Boat.Manufacturer`, `Boat.YearBuilt`, `Boat.RegistrationNumber` khỏi `Boat.cs`
- Xóa `Station.Address` nếu không dùng (không có trong target ERD)

**Performance:**
- Thêm index vào `CheckInEvent.ClientEventId` (đã có unique, OK)
- Thêm index `(TripId, VisitOrder)` trên `TripStopCall` (đã có unique, OK)
- Review `SearchTripsQuery`: xóa `Boat.TotalSeats` dependency; `AvailableSeatsCount` phải dùng segment-aware count (đếm SeatReservations active trong khoảng thời gian của trip, không phải `TotalSeats - TicketCount`)
- `Station.Latitude/Longitude`: đổi từ `double` thành `decimal` với `HasPrecision(10, 7)`

**Tests cần viết:**
1. `Phase1Tests/TicketCreationAfterPaymentTests.cs` — Verify ticket chỉ tạo sau IPN
2. `Phase1Tests/IdempotencyTests.cs` — IPN duplicate không tạo record thứ 2
3. `Phase2Tests/FareRulePricingTests.cs` — Giá tính đúng theo TripType × SeatClass
4. `Phase3Tests/TripStopCallSegmentTests.cs` — Overlap check dùng VisitOrder
5. `Phase5Tests/RefundCancelledTripTests.cs` — Refund đúng, không refund NoShow
6. `Phase5Tests/DuplicateRefundPreventionTests.cs`
7. `Phase6Tests/CheckInOnlineTests.cs` — Check-in đúng bến, đúng Trip
8. `Phase6Tests/CheckInOfflineSyncTests.cs` — Idempotent sync
9. `Phase6Tests/QrVerificationTests.cs` — Verify QR signature
10. `Phase7Tests/IncidentNotAutoChangeTripStatusTests.cs`

**Migration Phase 10:**
```bash
dotnet ef migrations add Phase10_CleanupAndPerformance \
  --project src/WaterbusSystem.Infrastructure \
  --startup-project src/WaterbusSystem.WebApi
```

**Final check:**
```bash
dotnet build
dotnet test
```

**Done when:** Build không lỗi. Tất cả test pass. Database schema khớp với target Logical ERD.

---

## Tóm tắt số lượng task

| Phase | Tasks | Số entities mới |
|-------|-------|----------------|
| 1 | TASK-01 → 12 | 0 (chỉ sửa) |
| 2 | TASK-13 → 20 | SeatClass, FareRule, PurchaseOrder |
| 3 | TASK-21 → 25 | RouteStop, ScheduleStop, TripStopCall |
| 4 | TASK-26 → 27 | 0 (chỉ commands) |
| 5 | TASK-28 → 31 | FinancialTransaction, RefundTicketAllocation |
| 6 | TASK-32 → 35 | ScannerDevice, ScannerAssignment, CheckInEvent |
| 7 | TASK-36 → 38 | Incident, IncidentTrip, TripOperationEvent |
| 8 | TASK-39 → 41 | 4 Profile entities, AccessGrant |
| 9 | TASK-42 → 44 | PointOfInterest, RoutePOI, AudioGuide, BoatPositionEvent |
| 10 | TASK-45 | 0 (cleanup) |
| **Tổng** | **45 tasks** | **21 entities mới** |
