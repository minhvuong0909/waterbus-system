using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using WaterbusSystem.Application;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.WebApi.BackgroundJobs;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.WebApi.Health;
using WaterbusSystem.WebApi.Middlewares;
using WaterbusSystem.WebApi.Realtime;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký các tầng kiến trúc Clean Architecture
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Cấu hình Controllers & JSON
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Filter validate request dùng chung cho Controller theo Clean Architecture thuần (không MediatR)
builder.Services.AddScoped<ValidateRequestFilter>();

// SignalR hubs + publisher realtime (BoatTracking, SeatSync)
builder.Services.AddRealtime();

// 3. Cấu hình Swagger UI có nút Authorize Bearer JWT
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Smart Waterbus System API",
        Version = "v1",
        Description = "Hệ thống Quản lý & Vận hành Bán vé Buýt Đường Sông kết hợp Du lịch Thông minh trên nền tảng .NET 8"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT Token theo định dạng: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 4. Cấu hình CORS: chỉ cho phép các domain Frontend/Mobile đã khai báo trong cấu hình
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCorsPolicy", policy =>
    {
        // AllowCredentials cần cho SignalR khi FE chạy khác origin (vd Vite dev :5173).
        // Hợp lệ vì dùng WithOrigins cụ thể, không phải AllowAnyOrigin.
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

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

    options.AddPolicy("AiAssistantPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // M11: Chặn brute-force đăng nhập. Luôn theo IP (Login luôn là anonymous request nên không có
    // User.Identity để partition theo tài khoản).
    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        // LƯU Ý: WriteAsJsonAsync(value, token) tự ghi đè ContentType về "application/json; charset=utf-8"
        // nếu không truyền contentType qua tham số - set Response.ContentType trước đó sẽ bị bỏ qua lặng lẽ.
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/v1/AiAssistant"))
        {
            await context.HttpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Quá nhiều yêu cầu tới trợ lý AI.",
                    Detail = "Vui lòng thử lại sau một phút.",
                    Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
                },
                options: null, contentType: "application/problem+json", cancellationToken: token);
            return;
        }
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/v1/Auth"))
        {
            await context.HttpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Quá nhiều yêu cầu đăng nhập.",
                    Detail = "Vui lòng thử lại sau một phút.",
                    Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
                },
                options: null, contentType: "application/problem+json", cancellationToken: token);
            return;
        }
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Quá nhiều yêu cầu. Vui lòng thử lại sau 1 phút.",
            statusCode = 429
        }, token);
    };
});

var app = builder.Build();

// 5. Khởi tạo CSDL và Seed dữ liệu mẫu khi khởi động
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();

    // M07: migration lỗi (không kết nối được, hoặc bị từ chối vì dữ liệu cũ) PHẢI dừng ứng dụng,
    // không được nuốt lỗi rồi phục vụ trên schema cũ. Compose sẽ khởi động lại container sau khi SQL sẵn sàng.
    await initializer.InitialiseAsync();

    // C01: Seed dữ liệu mẫu (gồm tài khoản Admin/Captain/Staff mật khẩu cố định) chỉ chạy ở
    // Development, tránh tự tạo lại các tài khoản đặc quyền mặc định trên CSDL Production.
    // Seed chỉ là dữ liệu demo nên lỗi seed không làm dừng API.
    if (app.Environment.IsDevelopment())
    {
        try
        {
            await initializer.SeedAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Không nạp được dữ liệu mẫu (Seed Data); API vẫn chạy.");
        }
    }
}

// 6. Gắn kết Pipeline Middlewares
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLocalizationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Waterbus API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AppCorsPolicy");

// M15: UseAuthentication phải đứng trước UseRateLimiter, vì BookingPolicy/AuthPolicy partition
// theo HttpContext.User.Identity.IsAuthenticated (Program.cs) - nếu không, User luôn rỗng lúc
// limiter chạy và mọi request bị gom theo IP thay vì theo tài khoản đã đăng nhập.
app.UseAuthentication();
app.UseMiddleware<GuestAccessMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();


app.MapControllers();
app.MapRealtimeHubs();

// M07: readiness thật (SQL + schema); healthcheck của Docker dùng endpoint này thay vì chỉ kiểm cổng TCP.
app.MapGet("/health/ready", async (ApplicationDbContext context, CancellationToken cancellationToken) =>
{
    var result = await ReadinessCheck.RunAsync(context, cancellationToken);
    return result.IsReady
        ? Results.Ok(new { status = "Ready" })
        : Results.Json(new { status = "NotReady", reason = result.Reason }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();
app.UseBackgroundJobs();

app.Run();
