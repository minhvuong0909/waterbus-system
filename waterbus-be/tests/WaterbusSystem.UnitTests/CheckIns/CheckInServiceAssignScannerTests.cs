using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.Features.CheckIns;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.CheckIns;

/// <summary>
/// Test CheckInService.AssignScannerAsync đối chiếu BR-OFF-02: chỉ 1 Primary Offline Scanner hoạt động
/// cùng lúc cho cùng điểm dừng.
/// </summary>
public class CheckInServiceAssignScannerTests
{
    private static CheckInService CreateService(WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context) => new(
        new CheckInRepository(context), new WaterbusSystem.Infrastructure.Persistence.EfUnitOfWork(context),
        Mock.Of<ICurrentUserService>(), Mock.Of<IDateTimeService>(), new CheckInProcessor(new CheckInRepository(context)),
        new WaterbusSystem.Infrastructure.Persistence.SqlScannerAssignmentLock(context),
        Options.Create(new NoShowFinalizationOptions()), NullLogger<CheckInService>.Instance);

    private static (TestDbContextFactory.TripSeedData Seed, TripStopCall StopCall, ScannerDevice Device) SeedDeviceAndStop(
        WaterbusSystem.Infrastructure.Persistence.ApplicationDbContext context)
    {
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var device = new ScannerDevice { DeviceCode = "DEV-01", IsActive = true };
        context.ScannerDevices.Add(device);
        context.SaveChanges();
        return (seed, stopCall, device);
    }

    [Fact]
    public async Task AssignScannerAsync_WhenNoExistingAssignment_ShouldCreatePrimary()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        var service = CreateService(context);

        var now = DateTimeOffset.UtcNow;
        var result = await service.AssignScannerAsync(
            new AssignScannerRequest(device.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, now, now.AddHours(2)),
            CancellationToken.None);

        result.IsPrimaryOffline.Should().BeTrue();
        context.ScannerAssignments.Count().Should().Be(1);
    }

    [Fact]
    public async Task AssignScannerAsync_WhenAnotherPrimaryOverlapsSameStop_ShouldThrowConflictException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        var service = CreateService(context);

        var now = DateTimeOffset.UtcNow;
        await service.AssignScannerAsync(
            new AssignScannerRequest(device.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, now, now.AddHours(2)),
            CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();

        var act = () => service.AssignScannerAsync(
            new AssignScannerRequest(device2.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, now.AddMinutes(30), now.AddHours(3)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AssignScannerAsync_WhenAnotherPrimaryDoesNotOverlapInTime_ShouldSucceed()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        var service = CreateService(context);

        var now = DateTimeOffset.UtcNow;
        await service.AssignScannerAsync(
            new AssignScannerRequest(device.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, now, now.AddHours(1)),
            CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();

        // Ca sau, không chồng thời gian với ca trước.
        var result = await service.AssignScannerAsync(
            new AssignScannerRequest(device2.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, now.AddHours(1), now.AddHours(2)),
            CancellationToken.None);

        result.Should().NotBeNull();
        context.ScannerAssignments.Count().Should().Be(2);
    }

    [Fact]
    public async Task AssignScannerAsync_WhenMultipleNonPrimaryAssignmentsOverlap_ShouldAllSucceed()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        var service = CreateService(context);

        var now = DateTimeOffset.UtcNow;
        await service.AssignScannerAsync(
            new AssignScannerRequest(device.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, false, now, now.AddHours(2)),
            CancellationToken.None);

        var device2 = new ScannerDevice { DeviceCode = "DEV-02", IsActive = true };
        context.ScannerDevices.Add(device2);
        context.SaveChanges();

        var act = () => service.AssignScannerAsync(
            new AssignScannerRequest(device2.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, false, now, now.AddHours(2)),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AssignScannerAsync_WhenOverlappingPrimaryIsAtADifferentStop_ShouldSucceed()
    {
        // Review PR #58: khoá sp_getapplock mới thêm (IScannerAssignmentLock) khoá theo
        // TripStopCallId - 2 điểm dừng khác nhau không được chặn chéo nhau dù cùng khoảng thời gian.
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var stopCall1 = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 1);
        var stopCall2 = seed.Trip.TripStopCalls.Single(c => c.VisitOrder == 2);
        var device1 = new ScannerDevice { DeviceCode = "DEV-STOP-1", IsActive = true };
        var device2 = new ScannerDevice { DeviceCode = "DEV-STOP-2", IsActive = true };
        context.ScannerDevices.AddRange(device1, device2);
        context.SaveChanges();
        var service = CreateService(context);

        var now = DateTimeOffset.UtcNow;
        await service.AssignScannerAsync(
            new AssignScannerRequest(device1.Id, TestDbContextFactory.AddScannerStaff(context), stopCall1.Id, true, now, now.AddHours(2)),
            CancellationToken.None);

        var act = () => service.AssignScannerAsync(
            new AssignScannerRequest(device2.Id, TestDbContextFactory.AddScannerStaff(context), stopCall2.Id, true, now, now.AddHours(2)),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        context.ScannerAssignments.Count().Should().Be(2);
    }

    [Fact]
    public async Task AssignScannerAsync_WhenDeviceIsInactive_ShouldThrowConflictException()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stopCall, device) = SeedDeviceAndStop(context);
        device.IsActive = false;
        context.SaveChanges();

        var service = CreateService(context);
        var act = () => service.AssignScannerAsync(
            new AssignScannerRequest(device.Id, TestDbContextFactory.AddScannerStaff(context), stopCall.Id, true, DateTimeOffset.UtcNow, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
    [Theory]
    [InlineData(null, false)]
    [InlineData("Passenger", true)]
    [InlineData("Staff", false)]
    public async Task AssignScannerAsync_RejectsIneligibleStaff(string? role, bool active)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stop, device) = SeedDeviceAndStop(context);
        var staff = role is null ? Guid.NewGuid() : TestDbContextFactory.AddScannerStaff(context, isActive: active, roleName: role);

        var assign = () => CreateService(context).AssignScannerAsync(
            new AssignScannerRequest(device.Id, staff, stop.Id, false, DateTimeOffset.UtcNow, null));

        await assign.Should().ThrowAsync<WaterbusSystem.Application.Common.Exceptions.ValidationException>();
        context.ScannerAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignScannerAsync_RejectsMissingDevice()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var (_, stop, _) = SeedDeviceAndStop(context);
        var staff = TestDbContextFactory.AddScannerStaff(context);
        var assign = () => CreateService(context).AssignScannerAsync(
            new AssignScannerRequest(Guid.NewGuid(), staff, stop.Id, false, DateTimeOffset.UtcNow, null));

        await assign.Should().ThrowAsync<NotFoundException>();
    }

}
