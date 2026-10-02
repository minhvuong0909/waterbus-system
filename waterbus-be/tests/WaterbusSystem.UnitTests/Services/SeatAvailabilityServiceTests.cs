using FluentAssertions;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Services;

/// <summary>
/// Test thuật toán kiểm tra đụng độ chặng di chuyển (Segment Overlap) gọi TRỰC TIẾP
/// SeatAvailabilityService thật (chạy trên EF Core InMemory DbContext), thay vì tự viết lại
/// công thức trong file test như trước đây (không bảo vệ được code thật).
/// Quy tắc: 2 khoảng nửa mở [A, B) và [C, D) giao thoa nhau khi và chỉ khi: A < D VÀ B > C
/// </summary>
public class SeatAvailabilityServiceTests
{
    [Theory]
    [InlineData(1, 2, 2, 5, true)]  // Liền kề trước: khách xuống tại trạm 2, khách sau lên tại trạm 2 => Không đụng độ
    [InlineData(5, 7, 2, 5, true)]  // Liền kề sau: khách trước xuống tại trạm 5, khách sau lên tại trạm 5 => Không đụng độ
    [InlineData(1, 2, 3, 5, true)]  // Cách xa phía trước => Không đụng độ
    [InlineData(6, 8, 2, 5, true)]  // Cách xa phía sau => Không đụng độ
    [InlineData(2, 4, 2, 5, false)] // Trùng bến đón, xuống sớm hơn => ĐỤNG ĐỘ
    [InlineData(3, 5, 2, 5, false)] // Lên muộn hơn, trùng bến xuống => ĐỤNG ĐỘ
    [InlineData(1, 6, 2, 5, false)] // Bao trùm toàn bộ chặng cũ => ĐỤNG ĐỘ
    [InlineData(3, 4, 2, 5, false)] // Nằm lọt bên trong chặng cũ => ĐỤNG ĐỘ
    [InlineData(2, 5, 2, 5, false)] // Trùng khớp hoàn toàn chặng => ĐỤNG ĐỘ
    public async Task IsSeatAvailableAsync_ShouldEvaluateSegmentOverlapCorrectly(
        int requestedBoarding, int requestedDisembarking,
        int existingBoarding, int existingDisembarking,
        bool expectedAvailable)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        context.SeatReservations.Add(new SeatReservation
        {
            TripId = seed.Trip.Id,
            SeatId = seed.Seat1.Id,
            BoardingStopOrder = existingBoarding,
            DisembarkingStopOrder = existingDisembarking,
            Status = ReservationStatus.Pending
        });
        context.SaveChanges();

        var service = new SeatAvailabilityService(context);

        var isAvailable = await service.IsSeatAvailableAsync(
            seed.Trip.Id, seed.Seat1.Id, requestedBoarding, requestedDisembarking, CancellationToken.None);

        isAvailable.Should().Be(expectedAvailable);
    }

    [Fact]
    public async Task IsSeatAvailableAsync_WhenExistingReservationIsCancelled_ShouldIgnoreIt()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        context.SeatReservations.Add(new SeatReservation
        {
            TripId = seed.Trip.Id,
            SeatId = seed.Seat1.Id,
            BoardingStopOrder = 1,
            DisembarkingStopOrder = 3,
            Status = ReservationStatus.Cancelled // Đã hủy -> không còn chiếm chỗ
        });
        context.SaveChanges();

        var service = new SeatAvailabilityService(context);

        var isAvailable = await service.IsSeatAvailableAsync(
            seed.Trip.Id, seed.Seat1.Id, 1, 3, CancellationToken.None);

        isAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetAvailableSeatsAsync_ShouldExcludeOnlyConflictingSeats()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);

        // Seat1 đã có người giữ chặng [1,3)
        context.SeatReservations.Add(new SeatReservation
        {
            TripId = seed.Trip.Id,
            SeatId = seed.Seat1.Id,
            BoardingStopOrder = 1,
            DisembarkingStopOrder = 3,
            Status = ReservationStatus.Pending
        });
        context.SaveChanges();

        var service = new SeatAvailabilityService(context);

        var availableSeats = await service.GetAvailableSeatsAsync(
            seed.Trip.Id, new List<Guid> { seed.Seat1.Id, seed.Seat2.Id }, 1, 3, CancellationToken.None);

        availableSeats.Should().ContainSingle().Which.Should().Be(seed.Seat2.Id);
    }
}
