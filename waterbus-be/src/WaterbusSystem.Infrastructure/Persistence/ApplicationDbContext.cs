using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Identity;

namespace WaterbusSystem.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của hệ thống, kết hợp ASP.NET Core Identity và Business Entities
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Áp dụng tự động toàn bộ IEntityTypeConfiguration<T> trong assembly hiện tại
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
