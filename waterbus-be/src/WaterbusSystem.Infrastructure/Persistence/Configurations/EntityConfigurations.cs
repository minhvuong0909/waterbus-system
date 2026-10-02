using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Infrastructure.Persistence.Configurations;

public class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DistanceKm).HasPrecision(10, 2);

        builder.HasOne(x => x.DepartureStation)
            .WithMany()
            .HasForeignKey(x => x.DepartureStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ArrivalStation)
            .WithMany()
            .HasForeignKey(x => x.ArrivalStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class BoatConfiguration : IEntityTypeConfiguration<Boat>
{
    public void Configure(EntityTypeBuilder<Boat> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CaptainUserId);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasMany(x => x.Seats)
            .WithOne(x => x.Boat)
            .HasForeignKey(x => x.BoatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.HasKey(x => x.Id);
        // Khóa Unique kép: Một tàu không thể có 2 ghế trùng mã (Ví dụ SWB-01 chỉ có 1 ghế F01)
        builder.HasIndex(x => new { x.BoatId, x.SeatCode }).IsUnique();
        builder.Property(x => x.SeatCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.PriceMultiplier).HasPrecision(5, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BasePrice).HasPrecision(18, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Route)
            .WithMany()
            .HasForeignKey(x => x.RouteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Boat)
            .WithMany()
            .HasForeignKey(x => x.BoatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.BookingCode).IsUnique();
        builder.Property(x => x.BookingCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CustomerEmail).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CustomerPhone).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.ManageOrderTokenHash).HasMaxLength(256);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasMany(x => x.Tickets)
            .WithOne(x => x.Booking)
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.PaymentTransactions)
            .WithOne(x => x.Booking)
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TicketCode).IsUnique();
        builder.Property(x => x.TicketCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.QrSeed).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Seat)
            .WithMany()
            .HasForeignKey(x => x.SeatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Provider).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DaysOfWeek).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Route)
            .WithMany()
            .HasForeignKey(x => x.RouteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SeatReservationConfiguration : IEntityTypeConfiguration<SeatReservation>
{
    public void Configure(EntityTypeBuilder<SeatReservation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TripId, x.SeatId });
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Trip)
            .WithMany()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Seat)
            .WithMany()
            .HasForeignKey(x => x.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Booking)
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
