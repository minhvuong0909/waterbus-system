using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Services.Checkout;

/// <summary>
/// SQL applock sở hữu bởi transaction, dùng chung giữa các instance và cả khi Redis mất kết nối.
/// Khóa thô có chủ ý cho scope môn học; không giữ khóa trong lúc gọi gateway bên ngoài.
/// </summary>
public sealed class SqlCommercialTransactionRunner(ApplicationDbContext context) : ICommercialTransactionRunner
{
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'waterbus:commercial',
                    @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                IF @result < 0 THROW 51001, 'Commercial transaction lock unavailable', 1;
                """, cancellationToken);
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is SqlException { Number: 51001 })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw new ConcurrencyException("Giao dịch đã thay đổi, vui lòng tải lại và thử lại.", ex);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
    }
}
