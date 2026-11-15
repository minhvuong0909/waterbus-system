using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Identity;

namespace WaterbusSystem.Infrastructure.Persistence;

/// <summary>
/// Khởi tạo và nạp dữ liệu mẫu ban đầu (Seeder) cho hệ thống Waterbus
/// </summary>
public class ApplicationDbContextInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<ApplicationDbContextInitializer> _logger;

    public ApplicationDbContextInitializer(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<ApplicationDbContextInitializer> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.MigrateAsync();
                await EnsureRolesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chạy Migration CSDL SQL Server.");
            throw;
        }
    }

    /// <summary>
    /// Role hệ thống là dữ liệu tham chiếu, luôn phải có (đăng ký khách hàng gán role Passenger). Khác với SeedAsync
    /// (tài khoản mẫu, chỉ Development) nên chạy ở mọi môi trường. Idempotent.
    /// </summary>
    private async Task EnsureRolesAsync()
    {
        foreach (var role in new[] { "Admin", "Captain", "Staff", "Passenger" })
        {
            if (!await _roleManager.RoleExistsAsync(role)) await _roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi nạp dữ liệu mẫu ban đầu (Seed Data).");
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        // 1. Seed Roles hệ thống
        var roles = new[] { "Admin", "Captain", "Staff", "Passenger" };
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole(role));
            }
        }

        // 2. Seed Tài khoản mặc định
        await SeedUserAsync("admin@waterbus.vn", "Admin Hệ Thống", "Admin@123456!", "Admin");
        await SeedUserAsync("captain@waterbus.vn", "Thuyền Trưởng Mặc Định", "Captain@123456!", "Captain");
        await SeedUserAsync("captain2@waterbus.vn", "Thuyền Trưởng Tàu Thường", "Captain@123456!", "Captain");
        await SeedUserAsync("staff@waterbus.vn", "Nhân Viên Soát Vé Bến", "Staff@123456!", "Staff");

        // 3. Seed 5 Bến Tàu Chính Dọc Sông Sài Gòn
        // Toạ độ lấy từ OpenStreetMap (tuyến Saigon Waterbus số 1); CSDL cũ được sửa ở CorrectDemoStationCoordinatesAsync.
        if (!await _context.Stations.AnyAsync())
        {
            var stations = new List<Station>
            {
                new() { Code = "ST01", Name = "Bến Bạch Đằng", Address = "Số 10B Tôn Đức Thắng, Phường Bến Nghé, Quận 1", Latitude = 10.7752301, Longitude = 106.7072821, OrderIndex = 1 },
                new() { Code = "ST02", Name = "Bến Bình An", Address = "Đường số 21, Phường Bình An, TP. Thủ Đức", Latitude = 10.79736, Longitude = 106.7283885, OrderIndex = 2 },
                new() { Code = "ST03", Name = "Bến Thanh Đa", Address = "Phường 27, Quận Bình Thạnh", Latitude = 10.8196062, Longitude = 106.7166367, OrderIndex = 3 },
                new() { Code = "ST04", Name = "Bến Hiệp Bình Chánh", Address = "Đường số 10, Hiệp Bình Chánh, TP. Thủ Đức", Latitude = 10.8270491, Longitude = 106.7316735, OrderIndex = 4 },
                new() { Code = "ST05", Name = "Bến Linh Đông", Address = "Đường Kha Vạn Cân, Phường Linh Đông, TP. Thủ Đức", Latitude = 10.8349605, Longitude = 106.7464066, OrderIndex = 5 }
            };

            await _context.Stations.AddRangeAsync(stations);
            await _context.SaveChangesAsync();

            // 4. Seed Tuyến 01: Bạch Đằng ⇄ Linh Đông
            var stBachDang = stations[0];
            var stLinhDong = stations[4];

            var route = new Route
            {
                Code = "RT01",
                Name = "Bạch Đằng - Linh Đông",
                DepartureStationId = stBachDang.Id,
                ArrivalStationId = stLinhDong.Id,
                EstimatedDurationMinutes = 45,
                DistanceKm = 10.8m,
                ServiceType = "Regular",
                IsActive = true
            };

            await _context.Routes.AddAsync(route);
            await _context.SaveChangesAsync();
        }

        // 5. Seed SeatClass nếu chưa có
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

        // 6. Seed Tàu Mẫu SWB-01 & 60 Ghế
        if (!await _context.Boats.AnyAsync())
        {
            var boat = new Boat
            {
                Code = "SWB-01",
                Name = "Saigon Waterbus 01",
                RegistrationNumber = "SG-8899-WS",
                TotalSeats = 60,
                Manufacturer = "Saigon Shipyard",
                YearBuilt = 2024,
                IsActive = true
            };

            // Tạo 60 ghế cố định theo layout 3 khoang
            // Khoang trước (VIP): 12 ghế (F01 -> F12)
            for (int i = 1; i <= 12; i++)
            {
                boat.Seats.Add(new Seat
                {
                    SeatCode = $"F{i:D2}",
                    SeatClassId = frontCabinId,
                    RowNumber = (i - 1) / 4 + 1,
                    ColumnNumber = (i - 1) % 4 + 1
                });
            }

            // Khoang tiêu chuẩn (Standard): 36 ghế (S01 -> S36)
            for (int i = 1; i <= 36; i++)
            {
                boat.Seats.Add(new Seat
                {
                    SeatCode = $"S{i:D2}",
                    SeatClassId = standardId,
                    RowNumber = (i - 1) / 4 + 1,
                    ColumnNumber = (i - 1) % 4 + 1
                });
            }

            // Boong ngoài trời (Outdoor): 12 ghế (O01 -> O12)
            for (int i = 1; i <= 12; i++)
            {
                boat.Seats.Add(new Seat
                {
                    SeatCode = $"O{i:D2}",
                    SeatClassId = outdoorId,
                    RowNumber = (i - 1) / 4 + 1,
                    ColumnNumber = (i - 1) % 4 + 1
                });
            }

            await _context.Boats.AddAsync(boat);
            await _context.SaveChangesAsync();
        }

        await AssignDemoCaptainAsync();
        await AssignSecondDemoCaptainAsync();

        // 7. Seed FareRule
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

        // 8. Seed RouteStops, Schedule, ScheduleStops, Trip
        if (!await _context.RouteStops.AnyAsync())
        {
            var stBachDang = await _context.Stations.FirstAsync(s => s.Code == "ST01");
            var stBinhAn = await _context.Stations.FirstAsync(s => s.Code == "ST02");
            var stThanhDa = await _context.Stations.FirstAsync(s => s.Code == "ST03");
            var stHiepBinhChanh = await _context.Stations.FirstAsync(s => s.Code == "ST04");
            var stLinhDong = await _context.Stations.FirstAsync(s => s.Code == "ST05");
            var route1 = await _context.Routes.FirstAsync(r => r.Code == "RT01");

            var routeStops = new List<RouteStop>
            {
                new() { RouteId = route1.Id, StationId = stBachDang.Id, SequenceNo = 1 },
                new() { RouteId = route1.Id, StationId = stBinhAn.Id, SequenceNo = 2 },
                new() { RouteId = route1.Id, StationId = stThanhDa.Id, SequenceNo = 3 },
                new() { RouteId = route1.Id, StationId = stHiepBinhChanh.Id, SequenceNo = 4 },
                new() { RouteId = route1.Id, StationId = stLinhDong.Id, SequenceNo = 5 }
            };
            await _context.RouteStops.AddRangeAsync(routeStops);
            await _context.SaveChangesAsync();
        }

        if (!await _context.Schedules.AnyAsync())
        {
            var route1 = await _context.Routes.FirstAsync(r => r.Code == "RT01");
            var schedule = new Schedule
            {
                RouteId = route1.Id,
                DepartureTime = new TimeSpan(8, 0, 0), // 8:00 AM
                IsActive = true
            };
            await _context.Schedules.AddAsync(schedule);
            await _context.SaveChangesAsync();
        }

        if (!await _context.ScheduleStops.AnyAsync())
        {
            var schedule = await _context.Schedules.FirstAsync();
            var routeStops = await _context.RouteStops.OrderBy(r => r.SequenceNo).ToListAsync();
            
            var scheduleStops = new List<ScheduleStop>();
            for (var i = 0; i < routeStops.Count; i++)
            {
                var rs = routeStops[i];
                var (arrival, departure) = DemoStopOffsets(i, routeStops.Count);
                scheduleStops.Add(new ScheduleStop
                {
                    ScheduleId = schedule.Id,
                    RouteStopId = rs.Id,
                    VisitOrder = rs.SequenceNo,
                    ArrivalOffsetMin = arrival,
                    DepartureOffsetMin = departure
                });
            }
            await _context.ScheduleStops.AddRangeAsync(scheduleStops);
            await _context.SaveChangesAsync();
        }

        if (!await _context.Trips.AnyAsync())
        {
            var schedule = await _context.Schedules.FirstAsync();
            var route = await _context.Routes.FirstAsync();
            var boat = await _context.Boats.FirstAsync();
            
            var trip = new Trip
            {
                ScheduleId = schedule.Id,
                RouteId = route.Id,
                BoatId = boat.Id,
                DepartureTime = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(8),
                ArrivalTime = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(8).AddMinutes(45),
                SalesCloseAt = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(8).AddMinutes(-15),
                Status = WaterbusSystem.Domain.Enums.TripStatus.Scheduled,
                TripType = WaterbusSystem.Domain.Enums.TripType.Commuter
            };
            await _context.Trips.AddAsync(trip);
            await _context.SaveChangesAsync();
        }

        await CorrectDemoStationCoordinatesAsync();
        await AlignDemoStopTimesAsync();

        // 9. Nạp tuyến về RT02, thêm tàu demo, lịch trình mở rộng, địa danh du lịch (POI) & chuyến sắp tới
        await SeedRoute2AndStopsAsync();
        await SeedAdditionalBoatsAsync(frontCabinId, standardId, outdoorId);
        await SeedAdditionalSchedulesAsync();
        await EnsureSightseeingPoisAsync();
        await RetireSightseeingTourAsync();
        await RelabelUnsafeDemoTripsAsync();
        await SeedDemoTripsForUpcomingDaysAsync();
        await SeedSightseeingRoutesAsync();
        await SeedAudioClipsAsync();

        // Seed TripStopCalls cho mọi Trip chưa có điểm dừng (dựa trên ScheduleStop)
        var demoTrips = await _context.Trips
            .Where(t => t.ScheduleId != null && !t.TripStopCalls.Any())
            .ToListAsync();
        foreach (var trip in demoTrips)
        {
            var stops = await _context.ScheduleStops.Include(s => s.RouteStop)
                .Where(s => s.ScheduleId == trip.ScheduleId && s.RouteStop!.RouteId == trip.RouteId)
                .OrderBy(s => s.VisitOrder).ToListAsync();
            foreach (var stop in stops)
            {
                // Add qua DbSet (không qua navigation của Trip đã nạp từ DB): Id Guid được gán sẵn phía client
                // nên nếu thêm qua navigation, EF coi là bản ghi đã tồn tại và sinh UPDATE (0 dòng)
                // -> DbUpdateConcurrencyException ở mọi lần khởi động sau lần đầu.
                _context.TripStopCalls.Add(new TripStopCall
                {
                    TripId = trip.Id,
                    RouteStopId = stop.RouteStopId,
                    ScheduleStopId = stop.Id,
                    VisitOrder = stop.VisitOrder,
                    EstimatedArrivalTime = stop.ArrivalOffsetMin.HasValue
                        ? trip.DepartureTime.AddMinutes(stop.ArrivalOffsetMin.Value) : null,
                    EstimatedDepartureTime = stop.DepartureOffsetMin.HasValue
                        ? trip.DepartureTime.AddMinutes(stop.DepartureOffsetMin.Value) : null,
                    CheckInOpenAt = stop.DepartureOffsetMin.HasValue
                        ? trip.DepartureTime.AddMinutes(stop.DepartureOffsetMin.Value - 30)
                        : trip.DepartureTime.AddMinutes(-30),
                    CheckInCloseAt = stop.DepartureOffsetMin.HasValue
                        ? trip.DepartureTime.AddMinutes(stop.DepartureOffsetMin.Value)
                        : trip.DepartureTime,
                    Status = (trip.Status == TripStatus.EnRoute && stop.VisitOrder == 1) ? "Departed" : "Planned"
                });
            }
        }
        await _context.SaveChangesAsync();

        await ReserveSightseeingBoatAsync();
        await SeedDemoCaptainAttendanceAsync();
    }

    /// <summary>Tàu tham quan của demo: chỉ chạy SS01/SS02 để thuyền trưởng của tàu này luôn thấy chuyến tham quan.</summary>
    internal const string SightseeingDemoBoatCode = "SWB-01";

    /// <summary>Tàu chuyến thường của demo, có thuyền trưởng riêng (captain2@waterbus.vn).</summary>
    internal const string CommuterDemoBoatCode = "SWB-02";

    /// <summary>
    /// Gán thuyền trưởng thứ hai cho tàu chuyến thường SWB-02 để demo cả hai loại tàu. Chỉ gán khi tàu chưa có thuyền trưởng.
    /// </summary>
    private async Task AssignSecondDemoCaptainAsync()
    {
        var boat = await _context.Boats.FirstOrDefaultAsync(b => b.Code == CommuterDemoBoatCode && b.CaptainUserId == null);
        var captain = await _userManager.FindByEmailAsync("captain2@waterbus.vn");
        if (boat is null || captain is null) return;
        boat.CaptainUserId = captain.Id;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Chuyển các chuyến thường chưa chạy và chưa có khách của tàu tham quan SWB-01 sang tàu khác không trùng giờ (Admin phân tàu theo chuyến).
    /// Idempotent; không đụng chuyến đã có khách, đang chạy hay đã xong.
    /// </summary>
    private async Task ReserveSightseeingBoatAsync()
    {
        var reserved = await _context.Boats.FirstOrDefaultAsync(b => b.Code == SightseeingDemoBoatCode);
        if (reserved is null) return;
        var others = await _context.Boats.Where(b => b.IsActive && !b.IsDeleted && b.Id != reserved.Id).OrderBy(b => b.Code).ToListAsync();
        if (others.Count == 0) return;
        others = others.OrderBy(b => b.Code == CommuterDemoBoatCode ? 0 : 1).ToList();

        var commuter = await _context.Trips
            .Where(t => t.BoatId == reserved.Id && t.TripType == TripType.Commuter && !t.IsDeleted
                        && t.Status == TripStatus.Scheduled && t.ActualDepartureTime == null)
            .ToListAsync();
        foreach (var trip in commuter)
        {
            if (await _context.Bookings.AnyAsync(b => b.TripId == trip.Id)) continue;
            foreach (var boat in others)
            {
                var overlap = await _context.Trips.AnyAsync(t => t.BoatId == boat.Id && t.Id != trip.Id && !t.IsDeleted
                    && t.Status != TripStatus.Cancelled && t.DepartureTime < trip.ArrivalTime && t.ArrivalTime > trip.DepartureTime);
                if (overlap) continue;
                trip.BoatId = boat.Id;
                break;
            }
        }
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Demo (Development): điểm danh sẵn thuyền trưởng của hai tàu demo cho các chuyến sắp chạy, như thể Admin đã xác nhận có mặt, để demo
    /// chỉ cần thao tác trên web. Quy tắc "Admin xác nhận có mặt trước khi khởi hành" của hệ thống giữ nguyên; ở môi trường thật Admin tự điểm danh.
    /// </summary>
    private async Task SeedDemoCaptainAttendanceAsync()
    {
        var admin = await _userManager.FindByEmailAsync("admin@waterbus.vn");
        if (admin is null) return;
        var now = DateTimeOffset.UtcNow;
        var boats = await _context.Boats
            .Where(b => (b.Code == SightseeingDemoBoatCode || b.Code == CommuterDemoBoatCode) && b.CaptainUserId != null)
            .ToListAsync();
        foreach (var boat in boats)
        {
            var trips = await _context.Trips
                .Where(t => t.BoatId == boat.Id && !t.IsDeleted && (t.Status == TripStatus.Scheduled || t.Status == TripStatus.Boarding)
                            && t.DepartureTime > now.AddHours(-1) && t.DepartureTime < now.AddDays(4))
                .Select(t => new { t.Id, t.DepartureTime })
                .ToListAsync();
            var recorded = (await _context.Set<CaptainAttendance>()
                .Where(a => a.BoatId == boat.Id && a.AssignmentVersion == boat.CaptainAssignmentVersion)
                .Select(a => a.TripId).ToListAsync()).ToHashSet();
            foreach (var trip in trips.Where(t => !recorded.Contains(t.Id)))
            {
                _context.Set<CaptainAttendance>().Add(new CaptainAttendance
                {
                    TripId = trip.Id, BoatId = boat.Id, CaptainId = boat.CaptainUserId!.Value,
                    AssignmentVersion = boat.CaptainAssignmentVersion, RecordedByAdminId = admin.Id,
                    RecordedAt = now, RequiredAt = trip.DepartureTime.AddMinutes(-20), IsLate = false,
                    Reason = "Demo: điểm danh sẵn"
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    /// <summary>Thời gian chạy cả tuyến demo RT01; trùng khoảng Trip.DepartureTime → Trip.ArrivalTime của chuyến seed.</summary>
    internal const int DemoRouteDurationMin = 45;

    private const int DemoDwellMin = 2;

    /// <summary>
    /// Giờ đến / rời của bến thứ <paramref name="index"/> (từ 0) tính từ giờ khởi hành. Bến đầu rời đúng giờ
    /// khởi hành và bến cuối đến đúng giờ đến của chuyến, để giờ ở từng bến không lệch giờ của cả chuyến.
    /// </summary>
    internal static (int? Arrival, int? Departure) DemoStopOffsets(int index, int count, int durationMin = DemoRouteDurationMin)
    {
        var arrival = (int)Math.Round((double)durationMin * index / (count - 1), MidpointRounding.AwayFromZero);
        return (
            index == 0 ? null : arrival,
            index == count - 1 ? null : index == 0 ? 0 : arrival + DemoDwellMin);
    }

    /// <summary>
    /// Bản seed cũ để bến đầu rời +2 phút và bến cuối đến +44 phút, lệch giờ của chuyến (08:00 → 08:45).
    /// Chỉ chỉnh lịch RT01 còn đúng mẫu cũ, nên giờ do Admin tự sửa không bị ghi đè; chạy lại không đổi gì thêm.
    /// </summary>
    private async Task AlignDemoStopTimesAsync()
    {
        int?[] oldArrival = [null, 11, 22, 33, 44];
        int?[] oldDeparture = [2, 13, 24, 35, null];

        var schedules = await _context.ScheduleStops
            .Where(s => s.RouteStop!.Route!.Code == "RT01")
            .Select(s => s.ScheduleId)
            .Distinct()
            .ToListAsync();

        var changed = false;
        foreach (var scheduleId in schedules)
        {
            var stops = await _context.ScheduleStops
                .Where(s => s.ScheduleId == scheduleId)
                .OrderBy(s => s.VisitOrder)
                .ToListAsync();
            if (stops.Count != oldArrival.Length
                || !stops.Select(s => s.ArrivalOffsetMin).SequenceEqual(oldArrival)
                || !stops.Select(s => s.DepartureOffsetMin).SequenceEqual(oldDeparture))
            {
                continue;
            }

            for (var i = 0; i < stops.Count; i++)
            {
                (stops[i].ArrivalOffsetMin, stops[i].DepartureOffsetMin) = DemoStopOffsets(i, stops.Count);
            }

            // Các chuyến chưa chạy đã sinh giờ từ mẫu cũ: tính lại từ giờ khởi hành.
            var calls = await _context.TripStopCalls
                .Where(c => c.ScheduleStopId != null
                    && stops.Select(s => s.Id).Contains(c.ScheduleStopId.Value)
                    && c.Trip!.Status == WaterbusSystem.Domain.Enums.TripStatus.Scheduled)
                .Include(c => c.Trip)
                .ToListAsync();
            foreach (var call in calls)
            {
                var stop = stops.First(s => s.Id == call.ScheduleStopId);
                var start = call.Trip!.DepartureTime;
                call.EstimatedArrivalTime = stop.ArrivalOffsetMin.HasValue ? start.AddMinutes(stop.ArrivalOffsetMin.Value) : null;
                call.EstimatedDepartureTime = stop.DepartureOffsetMin.HasValue ? start.AddMinutes(stop.DepartureOffsetMin.Value) : null;
            }
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Toạ độ cũ (gần đúng) của 5 bến demo và toạ độ thật từ OpenStreetMap (tuyến Saigon Waterbus số 1).
    /// </summary>
    internal static readonly (string Code, double OldLat, double OldLng, double NewLat, double NewLng)[] DemoStationCoordinates =
    [
        ("ST01", 10.7725, 106.7067, 10.7752301, 106.7072821),
        ("ST02", 10.7850, 106.7210, 10.79736, 106.7283885),
        ("ST03", 10.8256, 106.7289, 10.8196062, 106.7166367),
        ("ST04", 10.8412, 106.7321, 10.8270491, 106.7316735),
        ("ST05", 10.8521, 106.7554, 10.8349605, 106.7464066)
    ];

    /// <summary>
    /// CSDL tạo từ seed cũ vẫn giữ toạ độ gần đúng (bến nằm lệch khỏi bờ sông). Sửa sang toạ độ thật,
    /// nhưng CHỈ khi bến còn đúng toạ độ seed cũ, để không ghi đè toạ độ Admin đã tự chỉnh. Chạy lại không đổi gì thêm.
    /// </summary>
    private async Task CorrectDemoStationCoordinatesAsync()
    {
        const double tolerance = 0.0001; // ~11 m: đủ để bỏ qua sai số lưu số thực
        var codes = DemoStationCoordinates.Select(c => c.Code).ToList();
        var stations = await _context.Stations.Where(s => !s.IsDeleted && codes.Contains(s.Code)).ToListAsync();

        var changed = false;
        foreach (var (code, oldLat, oldLng, newLat, newLng) in DemoStationCoordinates)
        {
            var station = stations.FirstOrDefault(s => s.Code == code);
            if (station is null
                || Math.Abs(station.Latitude - oldLat) > tolerance
                || Math.Abs(station.Longitude - oldLng) > tolerance)
            {
                continue;
            }

            station.Latitude = newLat;
            station.Longitude = newLng;
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gán thuyền trưởng mặc định cho tàu mẫu SWB-01 để Cổng thuyền trưởng (GPS, bắt đầu/cập bến, SOS) dùng được ngay.
    /// Chỉ gán khi tàu chưa có thuyền trưởng, không ghi đè phân công Admin đã đặt.
    /// </summary>
    private async Task AssignDemoCaptainAsync()
    {
        var boat = await _context.Boats.FirstOrDefaultAsync(b => b.Code == "SWB-01" && b.CaptainUserId == null);
        var captain = await _userManager.FindByEmailAsync("captain@waterbus.vn");
        if (boat is null || captain is null)
        {
            return;
        }

        boat.CaptainUserId = captain.Id;
        await _context.SaveChangesAsync();
    }

    private async Task SeedRoute2AndStopsAsync()
    {
        var route2 = await _context.Routes.FirstOrDefaultAsync(r => r.Code == "RT02");
        var stBachDang = await _context.Stations.FirstOrDefaultAsync(s => s.Code == "ST01");
        var stBinhAn = await _context.Stations.FirstOrDefaultAsync(s => s.Code == "ST02");
        var stThanhDa = await _context.Stations.FirstOrDefaultAsync(s => s.Code == "ST03");
        var stHiepBinhChanh = await _context.Stations.FirstOrDefaultAsync(s => s.Code == "ST04");
        var stLinhDong = await _context.Stations.FirstOrDefaultAsync(s => s.Code == "ST05");

        if (stBachDang is null || stLinhDong is null) return;

        if (route2 is null)
        {
            route2 = new Route
            {
                Code = "RT02",
                Name = "Linh Đông - Bạch Đằng",
                DepartureStationId = stLinhDong.Id,
                ArrivalStationId = stBachDang.Id,
                EstimatedDurationMinutes = 45,
                DistanceKm = 10.8m,
                ServiceType = "Regular",
                IsActive = true
            };
            await _context.Routes.AddAsync(route2);
            await _context.SaveChangesAsync();
        }

        if (!await _context.RouteStops.AnyAsync(rs => rs.RouteId == route2.Id))
        {
            var routeStops = new List<RouteStop>
            {
                new() { RouteId = route2.Id, StationId = stLinhDong.Id, SequenceNo = 1 },
                new() { RouteId = route2.Id, StationId = stHiepBinhChanh!.Id, SequenceNo = 2 },
                new() { RouteId = route2.Id, StationId = stThanhDa!.Id, SequenceNo = 3 },
                new() { RouteId = route2.Id, StationId = stBinhAn!.Id, SequenceNo = 4 },
                new() { RouteId = route2.Id, StationId = stBachDang.Id, SequenceNo = 5 }
            };
            await _context.RouteStops.AddRangeAsync(routeStops);
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedAdditionalBoatsAsync(Guid frontCabinId, Guid standardId, Guid outdoorId)
    {
        var boatDefs = new[]
        {
            ("SWB-02", "Saigon Waterbus 02", "SG-8900-WS"),
            ("SWB-03", "Saigon Waterbus 03", "SG-8901-WS")
        };

        foreach (var (code, name, regNum) in boatDefs)
        {
            if (await _context.Boats.AnyAsync(b => b.Code == code)) continue;

            var boat = new Boat
            {
                Code = code,
                Name = name,
                RegistrationNumber = regNum,
                TotalSeats = 60,
                Manufacturer = "Saigon Shipyard",
                YearBuilt = 2024,
                IsActive = true
            };

            for (int i = 1; i <= 12; i++)
            {
                boat.Seats.Add(new Seat
                {
                    SeatCode = $"F{i:D2}",
                    SeatClassId = frontCabinId,
                    RowNumber = (i - 1) / 4 + 1,
                    ColumnNumber = (i - 1) % 4 + 1
                });
            }

            for (int i = 1; i <= 36; i++)
            {
                boat.Seats.Add(new Seat
                {
                    SeatCode = $"S{i:D2}",
                    SeatClassId = standardId,
                    RowNumber = (i - 1) / 4 + 1,
                    ColumnNumber = (i - 1) % 4 + 1
                });
            }

            for (int i = 1; i <= 12; i++)
            {
                boat.Seats.Add(new Seat
                {
                    SeatCode = $"O{i:D2}",
                    SeatClassId = outdoorId,
                    RowNumber = (i - 1) / 4 + 1,
                    ColumnNumber = (i - 1) % 4 + 1
                });
            }

            await _context.Boats.AddAsync(boat);
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedAdditionalSchedulesAsync()
    {
        var route1 = await _context.Routes.FirstOrDefaultAsync(r => r.Code == "RT01");
        var route2 = await _context.Routes.FirstOrDefaultAsync(r => r.Code == "RT02");
        if (route1 is null || route2 is null) return;

        var times = new[]
        {
            new TimeSpan(7, 30, 0),
            new TimeSpan(8, 30, 0),
            new TimeSpan(10, 30, 0),
            new TimeSpan(14, 0, 0),
            new TimeSpan(16, 30, 0),
            new TimeSpan(17, 30, 0),
            new TimeSpan(19, 0, 0),
            new TimeSpan(20, 30, 0),
            new TimeSpan(21, 30, 0)
        };

        foreach (var route in new[] { route1, route2 })
        {
            var routeStops = await _context.RouteStops
                .Where(rs => rs.RouteId == route.Id)
                .OrderBy(rs => rs.SequenceNo)
                .ToListAsync();

            if (routeStops.Count == 0) continue;

            foreach (var time in times)
            {
                var exists = await _context.Schedules.AnyAsync(s => s.RouteId == route.Id && s.DepartureTime == time);
                if (exists) continue;

                var schedule = new Schedule
                {
                    RouteId = route.Id,
                    DepartureTime = time,
                    IsActive = true
                };
                await _context.Schedules.AddAsync(schedule);
                await _context.SaveChangesAsync();

                var scheduleStops = new List<ScheduleStop>();
                int offset = 0;
                foreach (var rs in routeStops)
                {
                    scheduleStops.Add(new ScheduleStop
                    {
                        ScheduleId = schedule.Id,
                        RouteStopId = rs.Id,
                        VisitOrder = rs.SequenceNo,
                        ArrivalOffsetMin = offset == 0 ? null : offset,
                        DepartureOffsetMin = rs.SequenceNo == routeStops.Last().SequenceNo ? null : offset + 2
                    });
                    offset += 11;
                }
                await _context.ScheduleStops.AddRangeAsync(scheduleStops);
                await _context.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Mã tour vòng cũ (A → B → C → A). Dự án chỉ có hai tuyến tham quan một chiều SS01 (Bạch Đằng → Linh Đông) và SS02 (ngược lại),
    /// nên tuyến này không còn được seed; ở DB đã có thì ngừng bán: Route ngừng hoạt động, chuyến chưa chạy và chưa có khách bị hủy.
    /// Chuyến đã có khách hoặc đã chạy không bị đụng (Admin xử lý). Chạy lại không đổi gì thêm.
    /// </summary>
    internal const string RetiredTourRouteCode = "TOUR01";

    private async Task RetireSightseeingTourAsync()
    {
        var route = await _context.Routes.FirstOrDefaultAsync(r => r.Code == RetiredTourRouteCode);
        if (route is null || !route.IsActive) return;

        route.IsActive = false;
        var open = await _context.Trips
            .Where(t => t.RouteId == route.Id && t.Status == TripStatus.Scheduled && t.ActualDepartureTime == null)
            .ToListAsync();
        foreach (var trip in open)
        {
            if (await _context.Bookings.AnyAsync(b => b.TripId == trip.Id)) continue;
            trip.Status = TripStatus.Cancelled;
        }
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Bản seed cũ gán Trip là Sightseeing theo giờ khởi hành (từ 17:00) ngay cả trên Route Regular, nên Trip không thể bán
    /// (loại chuyến lệch loại dịch vụ của Route). Chỉ sửa nhãn các Trip chứng minh được an toàn: chưa xuất bến, Schedule
    /// là Commuter, KHÔNG có Booking nào. Không xoá, không đụng Trip đã có khách; Trip có khách để Admin xử lý thủ công.
    /// Chạy lại không đổi gì thêm.
    /// </summary>
    private async Task RelabelUnsafeDemoTripsAsync()
    {
        var mismatched = await _context.Trips
            .Where(t => t.TripType == TripType.Sightseeing && t.Route!.ServiceType == "Regular"
                        && t.Status == TripStatus.Scheduled && t.ActualDepartureTime == null
                        && t.ScheduleId != null && t.Schedule!.TripType == TripType.Commuter)
            .ToListAsync();
        var relabelled = 0;
        foreach (var trip in mismatched)
        {
            if (await _context.Bookings.AnyAsync(b => b.TripId == trip.Id)) continue;
            trip.TripType = TripType.Commuter;
            relabelled++;
        }

        if (relabelled > 0) await _context.SaveChangesAsync();
        var needReview = mismatched.Count - relabelled;
        if (needReview > 0)
            _logger.LogWarning("{Count} chuyến demo có loại Sightseeing nhưng Route là Regular và đã có Booking; cần Admin xử lý thủ công.", needReview);
    }

    private async Task SeedDemoTripsForUpcomingDaysAsync()
    {
        var routes = await _context.Routes.Where(r => r.Code == "RT01" || r.Code == "RT02").ToListAsync();
        if (routes.Count == 0) return;

        var boats = await _context.Boats.Where(b => b.IsActive).ToListAsync();
        // SWB-01 là tàu tham quan của demo: chuyến thường chạy trên các tàu còn lại.
        if (boats.Count > 1) boats = boats.Where(b => b.Code != SightseeingDemoBoatCode).ToList();
        if (boats.Count == 0) return;

        var schedules = await _context.Schedules.Where(s => s.IsActive).ToListAsync();
        if (schedules.Count == 0) return;

        var vnNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var today = vnNow.Date;

        int boatIndex = 0;
        bool enRouteSeeded = await _context.Trips.AnyAsync(t => t.Status == TripStatus.EnRoute);

        // Tạo chuyến cho Hôm nay (0) và 3 ngày tiếp theo (1, 2, 3)
        for (int dayOffset = 0; dayOffset <= 3; dayOffset++)
        {
            var date = today.AddDays(dayOffset);

            foreach (var route in routes)
            {
                var routeSchedules = schedules.Where(s => s.RouteId == route.Id).OrderBy(s => s.DepartureTime).ToList();

                foreach (var sch in routeSchedules)
                {
                    var depVn = new DateTimeOffset(date.Year, date.Month, date.Day, sch.DepartureTime.Hours, sch.DepartureTime.Minutes, 0, TimeSpan.FromHours(7));
                    var depUtc = depVn.ToUniversalTime();

                    if (await _context.Trips.AnyAsync(t => t.RouteId == route.Id && t.DepartureTime == depUtc))
                    {
                        continue;
                    }

                    var boat = boats[boatIndex % boats.Count];
                    boatIndex++;

                    // Loại chuyến do cấu hình Schedule quyết định (BR-ROUTE-03), không suy từ giờ khởi hành.
                    var tripType = sch.TripType;
                    var durationMin = route.EstimatedDurationMinutes > 0 ? route.EstimatedDurationMinutes : DemoRouteDurationMin;

                    var isTodayPast = dayOffset == 0 && depVn < vnNow;
                    TripStatus status = TripStatus.Scheduled;
                    DateTimeOffset? actualDep = null;

                    if (isTodayPast)
                    {
                        if (!enRouteSeeded && route.Code == "RT01" && (vnNow - depVn).TotalMinutes < 60)
                        {
                            status = TripStatus.EnRoute;
                            actualDep = depUtc;
                            enRouteSeeded = true;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var trip = new Trip
                    {
                        ScheduleId = sch.Id,
                        RouteId = route.Id,
                        BoatId = boat.Id,
                        DepartureTime = depUtc,
                        ArrivalTime = depUtc.AddMinutes(durationMin),
                        SalesCloseAt = depUtc.AddMinutes(-15),
                        Status = status,
                        TripType = tripType,
                        ActualDepartureTime = actualDep
                    };

                    await _context.Trips.AddAsync(trip);
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Hai tuyến tham quan riêng (BR-ROUTE-03), mỗi tuyến chỉ 2 bến (không dừng bến giữa): SS01 Bạch Đằng → Linh Đông và SS02 ngược lại.
    /// Idempotent theo mã tuyến/lịch/chuyến; gắn các địa danh (POI) theo chiều đi của từng tuyến.
    /// </summary>
    /// <summary>
    /// Địa danh giới thiệu trên chuyến tham quan, theo thứ tự dọc tuyến SS01 (SS02 đi ngược lại). Toạ độ là ĐIỂM TRÊN ĐƯỜNG SÔNG demo
    /// (DemoRoutePaths RT01/SS01) gần địa danh thật nhất, nơi tàu đi ngang và phát audio: mốc trên bản đồ, điểm kích hoạt và đường đi trùng nhau.
    /// Địa danh thật: Landmark 81 (10.794028, 106.722139), Cầu Sài Gòn (10.798890, 106.727220), Thảo Điền (10.802250, 106.728000),
    /// Thanh Đa (10.805000, 106.735000), Bình Quới (10.812000, 106.738000); hai điểm sau nằm trong đất liền nên lấy điểm sông ở bến Thanh Đa
    /// và khúc sông gần Bình Quới. FE có bản sao để vẽ mốc ở waterbus-fe/src/constant/sightseeingPois.ts.
    /// </summary>
    private static readonly (string Name, string Description, decimal Latitude, decimal Longitude, int RadiusM)[] SightseeingPois =
    [
        ("Landmark 81", "Tòa nhà chọc trời cao nhất Việt Nam, biểu tượng của TP.HCM hiện đại bên bờ sông Sài Gòn.", 10.792276m, 106.725583m, 250),
        ("Cầu Sài Gòn", "Cây cầu lâu đời bắc qua sông Sài Gòn, nối quận Bình Thạnh với khu Thảo Điền.", 10.798951m, 106.727697m, 150),
        ("Khu vực Thảo Điền ven sông", "Khu dân cư xanh ven sông với nhiều biệt thự và quán cà phê.", 10.801870m, 106.727569m, 150),
        ("Bán đảo Thanh Đa", "Khu sinh thái xanh thanh bình được dòng sông Sài Gòn ôm trọn.", 10.819606m, 106.716637m, 250),
        ("Khu vực Bình Quới ven sông", "Vùng cây xanh yên tĩnh ven sông với nhiều điểm du lịch sinh thái.", 10.823449m, 106.729600m, 250)
    ];

    /// <summary>Địa danh của bản seed cũ (không nằm trên đường đi demo): ngừng hoạt động, không xoá.</summary>
    private static readonly string[] LegacyPoiNames =
    [
        "Cột cờ Thủ Ngữ & Bến Nhà Rồng", "Cầu Ba Son (Cầu Thủ Thiêm 2)", "Tòa tháp Landmark 81 & Vinhomes Central Park",
        "Bán đảo Thanh Đa & Bình Quới", "Cầu Bình Lợi lịch sử"
    ];

    /// <summary>Tạo/cập nhật các địa danh tham quan theo <see cref="SightseeingPois"/> và ngừng hoạt động địa danh seed cũ. Idempotent theo tên.</summary>
    private async Task<List<PointOfInterest>> EnsureSightseeingPoisAsync()
    {
        var wanted = SightseeingPois.Select(d => d.Name).ToList();
        var existing = await _context.PointsOfInterest.Where(p => !p.IsDeleted).ToListAsync();
        var ordered = new List<PointOfInterest>();
        foreach (var def in SightseeingPois)
        {
            var poi = existing.FirstOrDefault(p => p.Name == def.Name);
            if (poi is null)
            {
                poi = new PointOfInterest { Name = def.Name };
                _context.PointsOfInterest.Add(poi);
            }
            poi.Description = def.Description;
            poi.Latitude = def.Latitude;
            poi.Longitude = def.Longitude;
            poi.IsActive = true;
            ordered.Add(poi);
        }
        foreach (var legacy in existing.Where(p => LegacyPoiNames.Contains(p.Name) && !wanted.Contains(p.Name)))
        {
            legacy.IsActive = false;
        }
        await _context.SaveChangesAsync();
        return ordered;
    }

    /// <summary>
    /// Gắn đúng các địa danh tham quan vào một Route theo thứ tự dọc tuyến, kèm bán kính kích hoạt; địa danh khác (seed cũ) ngừng hoạt động.
    /// Idempotent. Địa danh được xét lần lượt theo DisplayOrder nên thứ tự phải khớp chiều đi của tuyến.
    /// </summary>
    private async Task EnsureRoutePoisAsync(Route route, IReadOnlyList<PointOfInterest> ordered)
    {
        var routePois = await _context.RoutePois.Where(rp => rp.RouteId == route.Id).ToListAsync();

        // Địa danh không còn trên đường đi ngừng hoạt động và dời thứ tự ra xa để nhường (RouteId, DisplayOrder) là UNIQUE.
        var staleOrder = 1000;
        foreach (var stale in routePois.Where(rp => !ordered.Any(p => p.Id == rp.PoiId)).OrderBy(rp => rp.DisplayOrder))
        {
            stale.IsActive = false;
            if (stale.DisplayOrder < 1000) stale.DisplayOrder = staleOrder++;
        }
        await _context.SaveChangesAsync();

        var order = 1;
        foreach (var poi in ordered)
        {
            var routePoi = routePois.FirstOrDefault(rp => rp.PoiId == poi.Id);
            if (routePoi is null)
            {
                routePoi = new RoutePoi { RouteId = route.Id, PoiId = poi.Id };
                _context.RoutePois.Add(routePoi);
                routePois.Add(routePoi);
            }
            routePoi.DisplayOrder = order++;
            routePoi.IsActive = true;
            routePoi.IsDeleted = false;
            routePoi.TriggerRadiusM = SightseeingPois.First(d => d.Name == poi.Name).RadiusM;
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedSightseeingRoutesAsync()
    {
        var bachDang = await _context.Stations.FirstOrDefaultAsync(s => s.Code == "ST01");
        var linhDong = await _context.Stations.FirstOrDefaultAsync(s => s.Code == "ST05");
        if (bachDang is null || linhDong is null) return;

        var boat = await _context.Boats.FirstOrDefaultAsync(b => b.Code == "SWB-01" && b.IsActive)
            ?? await _context.Boats.FirstOrDefaultAsync(b => b.IsActive);
        var pois = await EnsureSightseeingPoisAsync();

        var defs = new[]
        {
            (Code: "SS01", Name: "Bạch Đằng - Linh Đông (Tham quan)", From: bachDang, To: linhDong, Reverse: false),
            (Code: "SS02", Name: "Linh Đông - Bạch Đằng (Tham quan)", From: linhDong, To: bachDang, Reverse: true)
        };
        // Giờ khởi hành lệch phút so với chuyến thường để không trùng giờ trên cùng tàu.
        var times = new[] { new TimeSpan(9, 15, 0), new TimeSpan(12, 15, 0), new TimeSpan(15, 15, 0), new TimeSpan(18, 15, 0) };
        var vnNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));

        foreach (var def in defs)
        {
            var route = await _context.Routes.FirstOrDefaultAsync(r => r.Code == def.Code);
            if (route is null)
            {
                route = new Route
                {
                    Code = def.Code, Name = def.Name, DepartureStationId = def.From.Id, ArrivalStationId = def.To.Id,
                    EstimatedDurationMinutes = 45, DistanceKm = 10.8m, ServiceType = "Sightseeing", IsActive = true
                };
                await _context.Routes.AddAsync(route);
                await _context.SaveChangesAsync();
            }

            var routeStops = await _context.RouteStops.Where(rs => rs.RouteId == route.Id).OrderBy(rs => rs.SequenceNo).ToListAsync();
            if (routeStops.Count == 0)
            {
                routeStops =
                [
                    // Điểm đầu chỉ cho lên, điểm cuối chỉ cho xuống: vé là cả hành trình (SightseeingJourney, BR-ROUTE-05).
                    new RouteStop { RouteId = route.Id, StationId = def.From.Id, SequenceNo = 1, CanBoard = true, CanAlight = false },
                    new RouteStop { RouteId = route.Id, StationId = def.To.Id, SequenceNo = 2, CanBoard = false, CanAlight = true }
                ];
                await _context.RouteStops.AddRangeAsync(routeStops);
                await _context.SaveChangesAsync();
            }

            await EnsureRoutePoisAsync(route, def.Reverse ? pois.AsEnumerable().Reverse().ToList() : pois);

            foreach (var time in times)
            {
                var schedule = await _context.Schedules.FirstOrDefaultAsync(s => s.RouteId == route.Id && s.DepartureTime == time);
                if (schedule is null)
                {
                    schedule = new Schedule { RouteId = route.Id, DepartureTime = time, TripType = TripType.Sightseeing, IsActive = true };
                    await _context.Schedules.AddAsync(schedule);
                    await _context.SaveChangesAsync();
                    await _context.ScheduleStops.AddRangeAsync(
                        new ScheduleStop { ScheduleId = schedule.Id, RouteStopId = routeStops[0].Id, VisitOrder = 1, ArrivalOffsetMin = null, DepartureOffsetMin = 0 },
                        new ScheduleStop { ScheduleId = schedule.Id, RouteStopId = routeStops[1].Id, VisitOrder = 2, ArrivalOffsetMin = 40, DepartureOffsetMin = null });
                    await _context.SaveChangesAsync();
                }

                if (boat is null) continue;
                for (var dayOffset = 0; dayOffset <= 3; dayOffset++)
                {
                    var date = vnNow.Date.AddDays(dayOffset);
                    var depVn = new DateTimeOffset(date.Year, date.Month, date.Day, time.Hours, time.Minutes, 0, TimeSpan.FromHours(7));
                    if (dayOffset == 0 && depVn < vnNow) continue;
                    var depUtc = depVn.ToUniversalTime();
                    if (await _context.Trips.AnyAsync(t => t.RouteId == route.Id && t.DepartureTime == depUtc)) continue;

                    await _context.Trips.AddAsync(new Trip
                    {
                        ScheduleId = schedule.Id, RouteId = route.Id, BoatId = boat.Id,
                        DepartureTime = depUtc, ArrivalTime = depUtc.AddMinutes(45), SalesCloseAt = depUtc.AddMinutes(-15),
                        Status = TripStatus.Scheduled, TripType = TripType.Sightseeing
                    });
                }
                await _context.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Nạp clip MP3 (vi + en) của thông báo bến và địa danh từ SeedData/Audio (sinh bằng tools/audio/generate_audio.py) vào bảng AudioClips.
    /// Idempotent theo (Kind, SubjectId, LanguageCode); thiếu file/bến/địa danh thì bỏ qua mục đó.
    /// </summary>
    private async Task SeedAudioClipsAsync()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Persistence", "SeedData", "Audio");
        var manifestPath = Path.Combine(dir, "manifest.json");
        if (!File.Exists(manifestPath)) return;

        using var manifest = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var stations = await _context.Stations.Where(s => !s.IsDeleted).ToDictionaryAsync(s => s.Code, s => s.Id);
        var pois = await _context.PointsOfInterest.Where(p => !p.IsDeleted).ToDictionaryAsync(p => p.Name, p => p.Id);
        var existing = (await _context.AudioClips.Where(c => !c.IsDeleted)
            .Select(c => new { c.Kind, c.SubjectId, c.LanguageCode }).ToListAsync())
            .Select(c => (c.Kind, c.SubjectId, c.LanguageCode)).ToHashSet();

        foreach (var entry in manifest.RootElement.EnumerateArray())
        {
            if (!Enum.TryParse<AudioClipKind>(entry.GetProperty("kind").GetString(), out var kind)) continue;
            var subject = entry.GetProperty("subject").GetString()!;
            var subjectId = kind == AudioClipKind.Poi
                ? (pois.TryGetValue(subject, out var poiId) ? poiId : Guid.Empty)
                : (stations.TryGetValue(subject, out var stationId) ? stationId : Guid.Empty);
            if (subjectId == Guid.Empty) continue;

            foreach (var clip in entry.GetProperty("clips").EnumerateObject())
            {
                var file = Path.Combine(dir, $"{entry.GetProperty("id").GetString()}.{clip.Name}.mp3");
                if (!File.Exists(file) || !existing.Add((kind, subjectId, clip.Name))) continue;
                _context.AudioClips.Add(new AudioClip
                {
                    Kind = kind, SubjectId = subjectId, LanguageCode = clip.Name,
                    Text = clip.Value.GetProperty("text").GetString()!,
                    DurationSeconds = clip.Value.GetProperty("seconds").GetInt32(),
                    Data = await File.ReadAllBytesAsync(file), IsPublished = true
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedUserAsync(string email, string fullName, string password, string role)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true
            };

            if (role == "Captain")
            {
                user.CaptainStatus = CaptainStatus.Active;
                // Số giấy phép là duy nhất: mỗi tài khoản Captain demo một số riêng.
                user.CaptainLicenseNumber = email == "captain@waterbus.vn" ? "DEMO-NOT-A-REAL-LICENSE" : $"DEMO-NOT-A-REAL-LICENSE-{email.Split('@')[0].ToUpperInvariant()}";
                user.CaptainLicenseExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
            }
            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
            }
        }
        else if (role == "Captain" && existingUser.CaptainStatus is null)
        {
            // Tài khoản Captain demo tạo trước khi có quản lý nhân sự (CaptainStatus mới là cột sau này, rỗng): đăng nhập bị từ chối.
            // Chỉ sửa tài khoản demo này, không đụng Captain do Admin tạo.
            existingUser.CaptainStatus = CaptainStatus.Active;
            existingUser.CaptainLicenseNumber ??= "DEMO-NOT-A-REAL-LICENSE";
            existingUser.CaptainLicenseExpiresOn ??= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
            await _userManager.UpdateAsync(existingUser);
        }
    }
}
