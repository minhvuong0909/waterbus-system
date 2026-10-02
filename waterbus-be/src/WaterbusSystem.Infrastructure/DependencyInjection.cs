using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Infrastructure.BackgroundJobs;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Locking;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Services.Payment;

namespace WaterbusSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Thiếu cấu hình ConnectionStrings:DefaultConnection. Hãy cấu hình qua User Secrets (local) hoặc biến môi trường (deploy), không hard-code trong appsettings.json.");

        // 1. Đăng ký CSDL SQL Server với EF Core 8
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ApplicationDbContextInitializer>();

        // 2. Đăng ký ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // 3. Đăng ký Xác thực JWT Bearer
        // LƯU Ý: Secret BẮT BUỘC phải lấy từ cấu hình (User Secrets / biến môi trường).
        // Không dùng fallback hard-code vì JwtTokenGenerator cũng đọc đúng key này -> đảm bảo luôn đồng bộ,
        // tránh trường hợp 2 nơi fallback lệch nhau khiến token sinh ra không bao giờ validate được.
        var jwtSecret = configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException(
                "Thiếu cấu hình JwtSettings:Secret. Hãy cấu hình qua User Secrets (local) hoặc biến môi trường (deploy).");
        var jwtIssuer = configuration["JwtSettings:Issuer"] ?? "WaterbusSystemApi";
        var jwtAudience = configuration["JwtSettings:Audience"] ?? "WaterbusClients";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        // 3.1. Cấu hình Authorization Policies (Admin, Staff, Captain)
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("StaffOnly", policy => policy.RequireRole("Staff"));
            options.AddPolicy("CaptainOnly", policy => policy.RequireRole("Captain"));
            options.AddPolicy("StaffOrAdmin", policy => policy.RequireRole("Admin", "Staff"));
        });

        // 4. Đăng ký Redis Distributed Lock Service (Lớp phòng thủ 1 chống Double-booking)
        // Tự quản lý kết nối Redis nội bộ, tự động fallback (DummyLock) nếu Redis không sẵn sàng
        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        // 5. Đăng ký Dịch vụ cổng thanh toán VNPAY & Token Generator
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISeatAvailabilityService, SeatAvailabilityService>();

        // 6. BackgroundService dọn dẹp Booking giữ chỗ quá hạn 10 phút chưa thanh toán
        services.AddHostedService<ExpiredBookingCleanupService>();

        return services;

    }
}
