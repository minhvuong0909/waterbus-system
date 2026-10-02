using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.UnitTests.TestHelpers;

/// <summary>
/// Helper dựng ApplicationDbContext thật (EF Core InMemory provider) kèm dữ liệu mẫu,
/// dùng để test các Handler/Service chạm CSDL mà không cần SQL Server thật.
/// </summary>
public static class TestDbContextFactory
{
    public static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        // currentUserService = null -> CreatedBy/LastModifiedBy sẽ là null, không ảnh hưởng test
        return new ApplicationDbContext(options, currentUserService: null);
    }

    /// <summary>
    /// Seed 3 bến (OrderIndex 1,2,3), 1 Route Bến1 -> Bến3, 1 Boat 2 ghế, 1 Trip Scheduled ngày mai.
    /// </summary>
    public static TripSeedData SeedTrip(ApplicationDbContext context)
    {
        var stationA = new Station { Code = "ST01", Name = "Ben A", Address = "A", OrderIndex = 1, IsActive = true };
        var stationB = new Station { Code = "ST02", Name = "Ben B", Address = "B", OrderIndex = 2, IsActive = true };
        var stationC = new Station { Code = "ST03", Name = "Ben C", Address = "C", OrderIndex = 3, IsActive = true };
        context.Stations.AddRange(stationA, stationB, stationC);

        var route = new Route
        {
            Code = "RT01",
            Name = "Tuyen Test A-C",
            DepartureStationId = stationA.Id,
            ArrivalStationId = stationC.Id,
            EstimatedDurationMinutes = 30,
            DistanceKm = 5,
            IsActive = true
        };
        context.Routes.Add(route);

        var boat = new Boat
        {
            Code = "SWB-TEST",
            Name = "Test Boat",
            RegistrationNumber = "REG-TEST",
            TotalSeats = 2,
            Manufacturer = "Test",
            YearBuilt = 2024,
            IsActive = true
        };

        var seat1 = new Seat { BoatId = boat.Id, SeatCode = "S01", Category = SeatCategory.Standard, RowNumber = 1, ColumnNumber = 1, PriceMultiplier = 1.0m, IsActive = true };
        var seat2 = new Seat { BoatId = boat.Id, SeatCode = "S02", Category = SeatCategory.FrontCabin, RowNumber = 1, ColumnNumber = 2, PriceMultiplier = 1.2m, IsActive = true };
        boat.Seats.Add(seat1);
        boat.Seats.Add(seat2);
        context.Boats.Add(boat);

        var otherBoat = new Boat
        {
            Code = "SWB-OTHER",
            Name = "Other Boat",
            RegistrationNumber = "REG-OTHER",
            TotalSeats = 1,
            Manufacturer = "Test",
            YearBuilt = 2024,
            IsActive = true
        };
        var seatOnOtherBoat = new Seat { BoatId = otherBoat.Id, SeatCode = "X01", Category = SeatCategory.Standard, RowNumber = 1, ColumnNumber = 1, PriceMultiplier = 1.0m, IsActive = true };
        otherBoat.Seats.Add(seatOnOtherBoat);
        context.Boats.Add(otherBoat);

        var trip = new Trip
        {
            RouteId = route.Id,
            BoatId = boat.Id,
            DepartureTime = DateTimeOffset.UtcNow.AddDays(1),
            ArrivalTime = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
            TripType = TripType.Commuter,
            Status = TripStatus.Scheduled,
            BasePrice = 15000m
        };
        context.Trips.Add(trip);

        context.SaveChanges();

        return new TripSeedData(stationA, stationB, stationC, route, boat, seat1, seat2, otherBoat, seatOnOtherBoat, trip);
    }

    public record TripSeedData(
        Station StationA,
        Station StationB,
        Station StationC,
        Route Route,
        Boat Boat,
        Seat Seat1,
        Seat Seat2,
        Boat OtherBoat,
        Seat SeatOnOtherBoat,
        Trip Trip);
}
