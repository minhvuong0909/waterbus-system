using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng truy xuất CSDL của hệ thống qua EF Core
/// </summary>
public interface IApplicationDbContext
{
    DbSet<SeatClass> SeatClasses { get; }
    DbSet<FareRule> FareRules { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<Station> Stations { get; }
    DbSet<Route> Routes { get; }
    DbSet<Boat> Boats { get; }
    DbSet<Seat> Seats { get; }
    DbSet<Schedule> Schedules { get; }
    DbSet<Trip> Trips { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<SeatReservation> SeatReservations { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

}
