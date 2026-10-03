using System.Threading.RateLimiting;
using Microsoft.OpenApi.Models;
using WaterbusSystem.Application;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký các tầng kiến trúc Clean Architecture
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Cấu hình Controllers & JSON
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
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

var app = builder.Build();

// 5. Khởi tạo CSDL và Seed dữ liệu mẫu khi khởi động
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();
    try
    {
        await initializer.InitialiseAsync();
        await initializer.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Chưa thể kết nối SQL Server để chạy Seed Data. Hãy chắc chắn SQL Server container đang chạy!");
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

app.UseRateLimiter();
app.UseMiddleware<GuestAccessMiddleware>();
app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();
