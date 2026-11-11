using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.WebApi.Health;

/// <summary>Kết quả kiểm tra sẵn sàng phục vụ: Reason chỉ có khi chưa sẵn sàng.</summary>
public sealed record ReadinessResult(bool IsReady, string? Reason);

/// <summary>
/// Audit M07: "cổng TCP mở" không có nghĩa API dùng được. Sẵn sàng = kết nối được SQL Server và
/// schema đã áp dụng đủ migration (nếu migration bị từ chối thì API không được báo healthy).
/// </summary>
public static class ReadinessCheck
{
    public static async Task<ReadinessResult> RunAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            return new ReadinessResult(false, "Không kết nối được cơ sở dữ liệu.");
        }

        // InMemory (test) không có khái niệm migration
        if (!context.Database.IsRelational()) return new ReadinessResult(true, null);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        return pending.Count == 0
            ? new ReadinessResult(true, null)
            : new ReadinessResult(false, $"Còn {pending.Count} migration chưa được áp dụng.");
    }
}
