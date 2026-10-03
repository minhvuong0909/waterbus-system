using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Identity;

namespace WaterbusSystem.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của hệ thống, kết hợp ASP.NET Core Identity và Business Entities
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<SeatClass> SeatClasses => Set<SeatClass>();
    public DbSet<FareRule> FareRules => Set<FareRule>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SeatReservation> SeatReservations => Set<SeatReservation>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<ScheduleStop> ScheduleStops => Set<ScheduleStop>();
    public DbSet<TripStopCall> TripStopCalls => Set<TripStopCall>();

        public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<RefundTicketAllocation> RefundTicketAllocations => Set<RefundTicketAllocation>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();
    public DbSet<ScannerDevice> ScannerDevices => Set<ScannerDevice>();
    public DbSet<ScannerAssignment> ScannerAssignments => Set<ScannerAssignment>();
    public DbSet<CheckInEvent> CheckInEvents => Set<CheckInEvent>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentTrip> IncidentTrips => Set<IncidentTrip>();
    public DbSet<TripOperationEvent> TripOperationEvents => Set<TripOperationEvent>();
    public DbSet<BoatPositionEvent> BoatPositionEvents => Set<BoatPositionEvent>();
    public DbSet<PointOfInterest> PointsOfInterest => Set<PointOfInterest>();
    public DbSet<RoutePoi> RoutePois => Set<RoutePoi>();
    public DbSet<AudioGuide> AudioGuides => Set<AudioGuide>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Áp dụng tự động toàn bộ IEntityTypeConfiguration<T> trong assembly hiện tại
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global Query Filter: tự động loại bỏ bản ghi đã xóa mềm (IsDeleted = true) khỏi MỌI truy vấn
        // của các entity kế thừa BaseEntity, tránh phải lặp lại thủ công "!x.IsDeleted" ở từng Handler
        // (dễ quên khi thêm Query mới). Dùng IgnoreQueryFilters() ở nơi thực sự cần xem cả bản ghi đã xóa.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeletedProperty = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var notDeleted = Expression.Not(isDeletedProperty);
            var lambda = Expression.Lambda(notDeleted, parameter);

            builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Tự động gán CreatedAt/CreatedBy khi thêm mới, UpdatedAt/LastModifiedBy khi chỉnh sửa,
    /// cho mọi entity kế thừa BaseEntity, dựa trên người dùng hiện tại (ICurrentUserService).
    /// </summary>
    private void ApplyAuditInformation()
    {
        var userId = _currentUserService?.UserId;
        var now = DateTimeOffset.UtcNow;

        foreach (EntityEntry<BaseEntity> entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.LastModifiedBy = userId;
                    break;
            }
        }
    }
}

