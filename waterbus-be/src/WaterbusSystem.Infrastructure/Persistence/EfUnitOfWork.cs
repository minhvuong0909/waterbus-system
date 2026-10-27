using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Infrastructure.Persistence;

public sealed class EfUnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Xung đột dữ liệu khi cập nhật ghế. Vui lòng thử lại!", ex);
        }
    }
}
