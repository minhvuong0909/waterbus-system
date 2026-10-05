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
        if (!await _context.Stations.AnyAsync())
        {
            var stations = new List<Station>
            {
                new() { Code = "ST01", Name = "Bến Bạch Đằng", Address = "Số 10B Tôn Đức Thắng, Phường Bến Nghé, Quận 1", Latitude = 10.7725, Longitude = 106.7067, OrderIndex = 1 },
                new() { Code = "ST02", Name = "Bến Bình An", Address = "Đường số 21, Phường Bình An, TP. Thủ Đức", Latitude = 10.7850, Longitude = 106.7210, OrderIndex = 2 },
                new() { Code = "ST03", Name = "Bến Thanh Đa", Address = "Phường 27, Quận Bình Thạnh", Latitude = 10.8256, Longitude = 106.7289, OrderIndex = 3 },
                new() { Code = "ST04", Name = "Bến Hiệp Bình Chánh", Address = "Đường số 10, Hiệp Bình Chánh, TP. Thủ Đức", Latitude = 10.8412, Longitude = 106.7321, OrderIndex = 4 },
                new() { Code = "ST05", Name = "Bến Linh Đông", Address = "Đường Kha Vạn Cân, Phường Linh Đông, TP. Thủ Đức", Latitude = 10.8521, Longitude = 106.7554, OrderIndex = 5 }
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

        // Seed only RT01 demo Trips from their actual ScheduleStop definitions.
        // Operational Trip generation and check-in window configuration belong to their own modules.
        var demoTrips = await _context.Trips
            .Where(t => t.Route!.Code == "RT01" && t.ScheduleId != null && !t.TripStopCalls.Any())
            .ToListAsync();
        foreach (var trip in demoTrips)
        {
            var stops = await _context.ScheduleStops.Include(s => s.RouteStop)
                .Where(s => s.ScheduleId == trip.ScheduleId && s.RouteStop!.RouteId == trip.RouteId)
                .OrderBy(s => s.VisitOrder).ToListAsync();
            foreach (var stop in stops)
            {
                trip.TripStopCalls.Add(new TripStopCall
                {
                    TripId = trip.Id,
                    RouteStopId = stop.RouteStopId,
                    ScheduleStopId = stop.Id,
                    VisitOrder = stop.VisitOrder,
                    EstimatedArrivalTime = stop.ArrivalOffsetMin.HasValue
                        ? trip.DepartureTime.AddMinutes(stop.ArrivalOffsetMin.Value) : null,
                    EstimatedDepartureTime = stop.DepartureOffsetMin.HasValue
                        ? trip.DepartureTime.AddMinutes(stop.DepartureOffsetMin.Value) : null
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

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
