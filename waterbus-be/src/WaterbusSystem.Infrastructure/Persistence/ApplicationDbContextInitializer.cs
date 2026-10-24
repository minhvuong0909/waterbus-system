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
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chạy Migration CSDL SQL Server.");
            throw;
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

        // 9. Nạp tuyến về RT02, thêm tàu demo, lịch trình mở rộng, địa danh du lịch (POI) & chuyến sắp tới
        await SeedRoute2AndStopsAsync();
        await SeedAdditionalBoatsAsync(frontCabinId, standardId, outdoorId);
        await SeedAdditionalSchedulesAsync();
        await SeedPointsOfInterestAsync();
        await SeedDemoTripsForUpcomingDaysAsync();

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

    private async Task SeedPointsOfInterestAsync()
    {
        if (await _context.PointsOfInterest.AnyAsync()) return;

        var pois = new List<PointOfInterest>
        {
            new()
            {
                Name = "Cột cờ Thủ Ngữ & Bến Nhà Rồng",
                Description = "Di tích lịch sử văn hóa lâu đời tại ngã ba kênh Bến Nghé và sông Sài Gòn, nơi Bác Hồ ra đi tìm đường cứu nước năm 1911.",
                Latitude = 10.7688000m,
                Longitude = 106.7072000m,
                IsActive = true
            },
            new()
            {
                Name = "Cầu Ba Son (Cầu Thủ Thiêm 2)",
                Description = "Cây cầu dây văng hiện đại hình cánh buồm nổi bật, kết nối trung tâm Quận 1 với khu đô thị mới Thủ Thiêm.",
                Latitude = 10.7816000m,
                Longitude = 106.7112000m,
                IsActive = true
            },
            new()
            {
                Name = "Tòa tháp Landmark 81 & Vinhomes Central Park",
                Description = "Tòa nhà chọc trời cao nhất Việt Nam (461.3m), biểu tượng cho sự thịnh vượng và năng động của TP.HCM.",
                Latitude = 10.7951000m,
                Longitude = 106.7218000m,
                IsActive = true
            },
            new()
            {
                Name = "Bán đảo Thanh Đa & Bình Quới",
                Description = "Khu vực sinh thái xanh thanh bình được bao bọc trọn vẹn bởi nhánh sông Sài Gòn uốn lượn.",
                Latitude = 10.8250000m,
                Longitude = 106.7230000m,
                IsActive = true
            },
            new()
            {
                Name = "Cầu Bình Lợi lịch sử",
                Description = "Cây cầu đường sắt vượt sông Sài Gòn đầu tiên xây dựng từ năm 1902, chứng nhân hơn một thế kỷ thăng trầm của thành phố.",
                Latitude = 10.8255000m,
                Longitude = 106.7145000m,
                IsActive = true
            }
        };

        await _context.PointsOfInterest.AddRangeAsync(pois);
        await _context.SaveChangesAsync();

        var routes = await _context.Routes.Where(r => r.Code == "RT01" || r.Code == "RT02").ToListAsync();
        foreach (var r in routes)
        {
            int order = 1;
            foreach (var poi in pois)
            {
                _context.RoutePois.Add(new RoutePoi
                {
                    RouteId = r.Id,
                    PoiId = poi.Id,
                    DisplayOrder = order++,
                    TriggerRadiusM = 350,
                    IsActive = true
                });
            }
        }

        int index = 1;
        foreach (var poi in pois)
        {
            _context.AudioGuides.Add(new AudioGuide
            {
                PoiId = poi.Id,
                LanguageCode = "vi",
                AudioAssetUri = $"/audio/poi-{index:D2}.mp3",
                DurationSeconds = 120,
                IsPublished = true
            });
            index++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedDemoTripsForUpcomingDaysAsync()
    {
        var routes = await _context.Routes.Where(r => r.Code == "RT01" || r.Code == "RT02").ToListAsync();
        if (routes.Count == 0) return;

        var boats = await _context.Boats.Where(b => b.IsActive).ToListAsync();
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

                    var tripType = sch.DepartureTime >= new TimeSpan(17, 0, 0)
                        ? TripType.Sightseeing
                        : TripType.Commuter;

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
                        ArrivalTime = depUtc.AddMinutes(45),
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

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
