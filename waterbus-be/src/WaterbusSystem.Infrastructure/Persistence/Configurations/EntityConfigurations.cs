using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Infrastructure.Persistence.Configurations;

public class SeatClassConfiguration : IEntityTypeConfiguration<SeatClass>
{
    public void Configure(EntityTypeBuilder<SeatClass> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class FareRuleConfiguration : IEntityTypeConfiguration<FareRule>
{
    public void Configure(EntityTypeBuilder<FareRule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TripType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Price).HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => new { x.TripType, x.SeatClassId, x.EffectiveFrom }).IsUnique();
        builder.HasOne(x => x.SeatClass)
            .WithMany(sc => sc.FareRules)
            .HasForeignKey(x => x.SeatClassId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PurchaserName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PurchaserEmail).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PurchaserPhone).HasMaxLength(20);
        builder.Property(x => x.PurchaseMode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.QuotedTotal).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

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
        builder.Property(x => x.ServiceType).HasMaxLength(20).IsRequired();
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
        
        // Unique partial index: 1 Captain chỉ được gán cho 1 Boat (nullable → dùng filter)
        builder.HasIndex(x => x.CaptainUserId)
            .IsUnique()
            .HasFilter("[CaptainUserId] IS NOT NULL");
            
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
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.SeatClass)
            .WithMany(sc => sc.Seats)
            .HasForeignKey(x => x.SeatClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Route)
            .WithMany()
            .HasForeignKey(x => x.RouteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Schedule)
            .WithMany()
            .HasForeignKey(x => x.ScheduleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Boat)
            .WithMany()
            .HasForeignKey(x => x.BoatId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.BookingCode).IsUnique();
        builder.Property(x => x.BookingCode).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => x.PublicBookingId).IsUnique();
        builder.Property(x => x.PublicBookingId).HasMaxLength(50).IsRequired();
        builder.Property(x => x.QrCredentialVersion).IsRequired();

        builder.HasOne(x => x.Order)
            .WithMany(o => o.Bookings)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CustomerEmail).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CustomerPhone).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.ManageOrderTokenHash).HasMaxLength(256);
        builder.Property(x => x.ManageOrderTokenExpiresAt);
        builder.Property(x => x.ManageOrderTokenRevoked).IsRequired().HasDefaultValue(false);
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
        builder.HasIndex(x => x.TicketCode).IsUnique();
        builder.HasIndex(x => x.SeatReservationId).IsUnique(); // 1:1
        builder.Property(x => x.TicketCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BoardingStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FareTripTypeSnapshot).HasMaxLength(20);
        builder.Property(x => x.FareSeatClassSnapshot).HasMaxLength(50);
        builder.Property(x => x.FaceFareSnapshot).HasPrecision(12, 2);
        builder.Property(x => x.PaidFareSnapshot).HasPrecision(12, 2);
        builder.HasOne(x => x.Booking).WithMany(b => b.Tickets)
            .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SeatReservation).WithOne(r => r.Ticket)
            .HasForeignKey<Ticket>(x => x.SeatReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
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
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
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
        builder.HasIndex(x => new { x.TripId, x.SeatId });
        builder.Property(x => x.PassengerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PassengerEmail).HasMaxLength(150);
        builder.Property(x => x.PassengerPhone).HasMaxLength(20);
        builder.Property(x => x.QuotedFare).HasPrecision(12, 2);
        builder.Property(x => x.PaidAllocation).HasPrecision(12, 2);
        builder.Property(x => x.SeatCodeAtSale).HasMaxLength(20);
        builder.Property(x => x.SeatClassAtSale).HasMaxLength(50);
        builder.HasOne(x => x.Booking).WithMany(b => b.SeatReservations)
            .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Trip).WithMany()
            .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Seat).WithMany()
            .HasForeignKey(x => x.SeatId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.RouteId, x.SequenceNo }).IsUnique();
        builder.HasOne(x => x.Route)
            .WithMany(r => r.Stops)
            .HasForeignKey(x => x.RouteId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Station)
            .WithMany()
            .HasForeignKey(x => x.StationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class ScheduleStopConfiguration : IEntityTypeConfiguration<ScheduleStop>
{
    public void Configure(EntityTypeBuilder<ScheduleStop> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ScheduleId, x.VisitOrder }).IsUnique();
        builder.HasOne(x => x.Schedule)
            .WithMany(s => s.Stops)
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RouteStop)
            .WithMany(rs => rs.ScheduleStops)
            .HasForeignKey(x => x.RouteStopId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class TripStopCallConfiguration : IEntityTypeConfiguration<TripStopCall>
{
    public void Configure(EntityTypeBuilder<TripStopCall> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TripId, x.VisitOrder }).IsUnique();
        builder.HasOne(x => x.Trip)
            .WithMany(t => t.TripStopCalls)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RouteStop)
            .WithMany(rs => rs.TripStopCalls)
            .HasForeignKey(x => x.RouteStopId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}


public class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.Property(x => x.TransactionType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.GatewayName).HasMaxLength(50);
        builder.Property(x => x.GatewayReference).HasMaxLength(100);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Order).WithMany()
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RefundBooking).WithMany()
            .HasForeignKey(x => x.RefundBookingId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginalPayment).WithMany(t => t.Refunds)
            .HasForeignKey(x => x.OriginalPaymentId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class RefundTicketAllocationConfiguration : IEntityTypeConfiguration<RefundTicketAllocation>
{
    public void Configure(EntityTypeBuilder<RefundTicketAllocation> builder)
    {
        builder.HasIndex(x => new { x.RefundTransactionId, x.TicketId }).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.HasOne(x => x.RefundTransaction).WithMany(t => t.RefundAllocations)
            .HasForeignKey(x => x.RefundTransactionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Ticket).WithMany()
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
    public void Configure(EntityTypeBuilder<AccessGrant> builder)
    {
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
        builder.Property(x => x.AccessType).HasMaxLength(30).IsRequired();
        builder.HasOne(x => x.Order).WithMany()
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Booking).WithMany()
            .HasForeignKey(x => x.BookingId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class ScannerDeviceConfiguration : IEntityTypeConfiguration<ScannerDevice>
{
    public void Configure(EntityTypeBuilder<ScannerDevice> builder)
    {
        builder.HasIndex(x => x.DeviceCode).IsUnique();
        builder.Property(x => x.DeviceCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(100);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class ScannerAssignmentConfiguration : IEntityTypeConfiguration<ScannerAssignment>
{
    public void Configure(EntityTypeBuilder<ScannerAssignment> builder)
    {
        builder.HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TripStopCall).WithMany()
            .HasForeignKey(x => x.TripStopCallId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class CheckInEventConfiguration : IEntityTypeConfiguration<CheckInEvent>
{
    public void Configure(EntityTypeBuilder<CheckInEvent> builder)
    {
        builder.HasIndex(x => x.ClientEventId).IsUnique();
        builder.Property(x => x.ClientEventId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SyncStatus).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.Ticket).WithMany()
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TripStopCall).WithMany()
            .HasForeignKey(x => x.TripStopCallId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScannerDevice).WithMany()
            .HasForeignKey(x => x.ScannerDeviceId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.Property(x => x.ScopeType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(30).IsRequired();
        builder.Property(x => x.IncidentType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.Route).WithMany()
            .HasForeignKey(x => x.RouteId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Boat).WithMany()
            .HasForeignKey(x => x.BoatId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class IncidentTripConfiguration : IEntityTypeConfiguration<IncidentTrip>
{
    public void Configure(EntityTypeBuilder<IncidentTrip> builder)
    {
        builder.HasIndex(x => new { x.IncidentId, x.TripId }).IsUnique();
        builder.Property(x => x.OperationDecision).HasMaxLength(20);
        builder.HasOne(x => x.Incident).WithMany(i => i.AffectedTrips)
            .HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Trip).WithMany()
            .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class TripOperationEventConfiguration : IEntityTypeConfiguration<TripOperationEvent>
{
    public void Configure(EntityTypeBuilder<TripOperationEvent> builder)
    {
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FromStatus).HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.Trip).WithMany()
            .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Incident).WithMany(i => i.OperationEvents)
            .HasForeignKey(x => x.IncidentId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class BoatPositionEventConfiguration : IEntityTypeConfiguration<BoatPositionEvent>
{
    public void Configure(EntityTypeBuilder<BoatPositionEvent> builder)
    {
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.Property(x => x.SpeedKnots).HasPrecision(6, 2);
        builder.Property(x => x.Source).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.Boat).WithMany()
            .HasForeignKey(x => x.BoatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Trip).WithMany()
            .HasForeignKey(x => x.TripId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    }
}
public class PointOfInterestConfiguration : IEntityTypeConfiguration<PointOfInterest>
{
    public void Configure(EntityTypeBuilder<PointOfInterest> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class RoutePoiConfiguration : IEntityTypeConfiguration<RoutePoi>
{
    public void Configure(EntityTypeBuilder<RoutePoi> builder)
    {
        builder.HasIndex(x => new { x.RouteId, x.DisplayOrder }).IsUnique();
        builder.HasOne(x => x.Route).WithMany()
            .HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Poi).WithMany(p => p.RoutePois)
            .HasForeignKey(x => x.PoiId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class AudioGuideConfiguration : IEntityTypeConfiguration<AudioGuide>
{
    public void Configure(EntityTypeBuilder<AudioGuide> builder)
    {
        builder.HasIndex(x => new { x.PoiId, x.LanguageCode }).IsUnique();
        builder.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.AudioAssetUri).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.Poi).WithMany(p => p.AudioGuides)
            .HasForeignKey(x => x.PoiId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
