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
