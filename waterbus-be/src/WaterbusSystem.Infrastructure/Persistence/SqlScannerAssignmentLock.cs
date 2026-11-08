using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Persistence;

namespace WaterbusSystem.Infrastructure.Persistence;

/// <summary>
/// SQL Server application lock (sp_getapplock, phạm vi Session) giữ trên đúng connection của
/// DbContext đang dùng - cùng pattern với SqlWeatherGuardRunLock, nhưng khoá theo 1 KEY
/// (TripStopCallId) thay vì 1 resource cố định duy nhất, để các điểm dừng khác không bị chặn chéo.
/// </summary>
public sealed class SqlScannerAssignmentLock(ApplicationDbContext context) : IScannerAssignmentLock
{
    private const string ResourcePrefix = "waterbus:scanner-assignment:";

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        Guid tripStopCallId, TimeSpan wait, CancellationToken cancellationToken = default)
    {
        // Test đơn vị dùng InMemory (không có SQL): không có gì để khoá
        if (!context.Database.IsRelational()) return new Lease(null, null);

        var resource = ResourcePrefix + tripStopCallId.ToString("N");

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = @timeout",
                [result, new SqlParameter("@resource", resource), new SqlParameter("@timeout", (int)wait.TotalMilliseconds)],
                cancellationToken);

            // 0 = có ngay, 1 = có sau khi chờ; âm = hết giờ / bị huỷ / deadlock
            if ((int)result.Value >= 0) return new Lease(context, resource);
        }
        catch
        {
            await context.Database.CloseConnectionAsync();
            throw;
        }

        await context.Database.CloseConnectionAsync();
        return null;
    }

    private sealed class Lease(ApplicationDbContext? context, string? resource) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (context is null) return;
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session'", new SqlParameter("@resource", resource));
            }
            finally
            {
                // Đóng connection cũng huỷ khoá Session nếu lệnh trên lỗi
                await context.Database.CloseConnectionAsync();
            }
        }
    }
}
