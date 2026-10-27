using WaterbusSystem.Application.Interfaces.Persistence;

namespace WaterbusSystem.Infrastructure.Persistence;

public sealed class EfUnitOfWork(
    ApplicationDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}