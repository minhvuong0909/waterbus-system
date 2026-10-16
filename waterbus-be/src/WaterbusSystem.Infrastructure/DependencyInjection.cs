using System.Text;
using Azure.Storage.Blobs;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Application.Features.Bookings.Commands.CreateBooking;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Features.Tracking;
using WaterbusSystem.Infrastructure.BackgroundJobs;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Locking;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.Infrastructure.Services.Payment;
using WaterbusSystem.Infrastructure.Services.Refunds;
using WaterbusSystem.Infrastructure.Services.AiBooking;
using WaterbusSystem.Infrastructure.Services.AudioStorage;
using WaterbusSystem.Infrastructure.Services.Tts;
using WaterbusSystem.Infrastructure.Services.Tracking;
using WaterbusSystem.Infrastructure.Services.Weather;

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

            // SignalR: đọc JWT từ query access_token cho đường dẫn /hubs
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = SignalRTokenReader.OnMessageReceived
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
        services.AddHttpClient<IVnPayService, VnPayService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISeatAvailabilityService, SeatAvailabilityService>();
        services.AddScoped<ICommercialTransactionRunner, SqlCommercialTransactionRunner>();
        services.AddScoped<BookingCreationService>();
        services.AddScoped<CheckoutCommitEffects>();
        services.AddScoped<WaterbusSystem.Application.Features.Payments.Services.PaymentConfirmationService>();
        services.AddScoped<CheckoutAccessService>();
        services.AddScoped<IBookingQrSigner, RsaBookingQrSigner>();
        services.AddSingleton(new CheckoutOptions
        {
            SessionTtlMinutes = configuration.GetValue("Checkout:SessionTtlMinutes", 10),
            PaymentPendingTtlMinutes = configuration.GetValue("Checkout:PaymentPendingTtlMinutes", 15),
            AccessTokenTtlDays = configuration.GetValue("Checkout:AccessTokenTtlDays", 30)
        });

        // 5.1 BR-RF-10: hai nhánh hoàn tiền. Ưu tiên gateway tự động (VNPAY); nếu gateway không hỗ trợ
        // hoặc lỗi không xử lý được, yêu cầu hoàn chuyển sang nhánh thủ công của Admin.
        // ManualRefundGateway đăng ký trước VnPayRefundGateway vì SelectGateway() chọn theo
        // SupportsAutomaticRefund chứ không theo thứ tự đăng ký.
        services.AddScoped<IRefundGateway, ManualRefundGateway>();
        services.AddScoped<IRefundGateway, VnPayRefundGateway>();

        // 5.2 BR-HOLD-01 → BR-HOLD-05: phiên giữ ghế tạm thời trên Redis cho khách không cần đăng nhập.
        // TTL cấu hình qua Checkout:SessionTtlMinutes và Checkout:PaymentPendingTtlMinutes.
        services.AddSingleton<ICheckoutSessionStore, RedisCheckoutSessionStore>();
        services.AddScoped<ICheckoutSessionService>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            return new CheckoutSessionService(
                sp.GetRequiredService<ICheckoutSessionStore>(),
                sp.GetRequiredService<IDateTimeService>(),
                sp.GetRequiredService<CheckoutOptions>().HoldDuration,
                sp.GetRequiredService<CheckoutOptions>().PaymentDuration);
        });

        // 6. AI Booking Assistant (OpenAI gpt-4.1-mini qua Semantic Kernel)
        // Mặc định ở trạng thái chưa kích hoạt (IsEnabled = false, ApiKey chưa cấu hình).
        // Phụ thuộc còn thiếu trước khi kích hoạt production:
        // - Owner Trip Search hoàn thiện FareRule pricing (hiện BasePrice = 0 TODO) và danh sách stops thực tế.
        // - Owner Web/Booking chốt hợp đồng URL đặt vé.
        // - Cấu hình OpenAI API Key qua User Secrets / biến môi trường.
        services.Configure<OpenAiAssistantOptions>(configuration.GetSection(OpenAiAssistantOptions.SectionName));
        // Adapter đã có để thử local; cả LLM và hai contract tích hợp đều tắt mặc định.
        services.Configure<AiBookingIntegrationOptions>(configuration.GetSection(AiBookingIntegrationOptions.SectionName));
        services.AddScoped<ITripSearchTool, MediatRTripSearchTool>();
        services.AddScoped<IBookingLinkTool, ConfiguredBookingLinkTool>();
        services.AddScoped<IAiBookingAssistantService, SemanticKernelAiBookingAssistantService>();

        // 7. Tính khoảng cách địa lý cho geofence POI (NetTopologySuite, stateless)
        services.AddSingleton<IGeoDistanceCalculator, NtsGeoDistanceCalculator>();

        // 8. Azure AI Speech Text-to-Speech (Neural Voice MP3 tiếng Việt)
        services.Configure<AzureSpeechOptions>(configuration.GetSection(AzureSpeechOptions.SectionName));
        services.AddTransient<IAzureSpeechSynthesizerClient, DefaultAzureSpeechSynthesizerClient>();
        services.AddScoped<ITextToSpeechService, AzureSpeechTtsService>();

        // Kho MP3 private. Chỉ khởi tạo client khi chức năng lưu audio được gọi;
        // ứng dụng vẫn khởi động được trước khi team cấp Azure Storage credential.
        services.Configure<AzureBlobAudioStorageOptions>(configuration.GetSection(AzureBlobAudioStorageOptions.SectionName));
        services.AddScoped<IAudioStorageService>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureBlobAudioStorageOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.ConnectionString) || string.IsNullOrWhiteSpace(options.ContainerName))
                throw new InvalidOperationException("Thiếu AzureBlobAudioStorage:ConnectionString hoặc ContainerName. Cấu hình secret ngoài Git trước khi lưu MP3.");

            var container = new BlobContainerClient(options.ConnectionString, options.ContainerName);
            return new AzureBlobAudioStorageService(
                container,
                provider.GetRequiredService<FluentValidation.IValidator<WaterbusSystem.Application.Common.Models.StoreAudioCommand>>(),
                provider.GetRequiredService<ILogger<AzureBlobAudioStorageService>>());
        });

        // 9. BackgroundService dọn dẹp Booking giữ chỗ quá hạn 10 phút chưa thanh toán
        services.AddHostedService<ExpiredBookingCleanupService>();

        // 7. Theo dõi vị trí tàu: cache vị trí mới nhất (Redis) + giả lập GPS cho demo (mặc định tắt)
        services.Configure<TrackingOptions>(configuration.GetSection(TrackingOptions.SectionName));
        services.AddSingleton<IBoatPositionCache, RedisBoatPositionCache>();
        services.AddHostedService<BoatSimulatorService>();

        // 8. Hangfire (lưu job trong chính SQL Server, schema HangFire) + Weather Guard
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SqlClientFactory = Microsoft.Data.SqlClient.SqlClientFactory.Instance,
                PrepareSchemaIfNecessary = true
            }));
        services.AddHangfireServer(options => options.WorkerCount = 2);
        services.AddScoped<WeatherGuardJob>();

        services.Configure<WeatherOptions>(configuration.GetSection(WeatherOptions.SectionName));
        var weatherProvider = configuration[$"{WeatherOptions.SectionName}:Provider"];
        if (string.Equals(weatherProvider, "OpenWeatherMap", StringComparison.OrdinalIgnoreCase))
        {
            var timeoutSeconds = configuration.GetValue($"{WeatherOptions.SectionName}:TimeoutSeconds", 10);
            var baseUrl = configuration[$"{WeatherOptions.SectionName}:BaseUrl"] ?? new WeatherOptions().BaseUrl;
            services.AddHttpClient<IWeatherProvider, OpenWeatherMapProvider>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
        }
        else
        {
            services.AddSingleton<IWeatherProvider, FakeWeatherProvider>();
        }

        return services;

    }

    /// <summary>
    /// BR-HOLD-05: TTL của phiên giữ ghế và cửa sổ PaymentPending là tham số cấu hình do
    /// Admin/hệ thống quy định, không phải hằng số nghiệp vụ bất biến.
    /// </summary>
    private static TimeSpan ReadMinutes(IConfiguration configuration, string key, int fallbackMinutes)
    {
        var raw = configuration[key];
        return int.TryParse(raw, out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : TimeSpan.FromMinutes(fallbackMinutes);
    }
}
