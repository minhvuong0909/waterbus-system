using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Persistence;

namespace WaterbusSystem.Infrastructure.Persistence;

/// <summary>
/// SQL Server application lock (sp_getapplock, phạm vi Session) giữ trên đúng connection của DbContext đang dùng.
/// Không cần migration và hoạt động ngay cả khi Redis tắt (SQL là nguồn chân lý của Incident).
/// </summary>
public sealed class SqlWeatherGuardRunLock(ApplicationDbContext context) : IWeatherGuardRunLock
{
    private const string Resource = "waterbus:weather-guard";

    public async Task<IAsyncDisposable?> TryAcquireAsync(TimeSpan wait, CancellationToken cancellationToken = default)
    {
        // Test đơn vị dùng InMemory (không có SQL): không có gì để khoá
        if (!context.Database.IsRelational()) return new Lease(null);

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = @timeout",
                [result, new SqlParameter("@resource", Resource), new SqlParameter("@timeout", (int)wait.TotalMilliseconds)],
                cancellationToken);

            // 0 = có ngay, 1 = có sau khi chờ; âm = hết giờ / bị huỷ / deadlock
            if ((int)result.Value >= 0) return new Lease(context);
        }
        catch
        {
            await context.Database.CloseConnectionAsync();
            throw;
        }

        await context.Database.CloseConnectionAsync();
        return null;
    }

    private sealed class Lease(ApplicationDbContext? context) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (context is null) return;
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session'", new SqlParameter("@resource", Resource));
            }
            finally
            {
                // Đóng connection cũng huỷ khoá Session nếu lệnh trên lỗi
                await context.Database.CloseConnectionAsync();
            }
        }
    }
}
