using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.Features.ScannerDevices.Services;
using WaterbusSystem.Application.Validators.ScannerDevices;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.UnitTests.CheckIns;

public class ScannerDeviceAdminServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 3, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = TestDbContextFactory.CreateDbContext();
    private readonly TestDbContextFactory.TripSeedData _seed;
    private readonly Mock<IDateTimeService> _clock = new();

    public ScannerDeviceAdminServiceTests()
    {
        _clock.Setup(c => c.Now).Returns(() => Now);
        _seed = TestDbContextFactory.SeedTrip(_context);
    }

    private ScannerDeviceAdminService Service() =>
        new(new ScannerDeviceAdminRepository(_context), new EfUnitOfWork(_context), new DirectCommercialTransactionRunner(), _clock.Object);

    private ScannerAssignment Assign(Guid deviceId, DateTimeOffset? until)
    {
        var assignment = new ScannerAssignment
        {
            DeviceId = deviceId, StaffAccountId = Guid.NewGuid(), TripStopCallId = _seed.Trip.TripStopCalls.First().Id,
            IsPrimaryOffline = true, ActiveFrom = Now.AddHours(-1), ActiveUntil = until
        };
        _context.ScannerAssignments.Add(assignment);
        _context.SaveChanges();
        return assignment;
    }

    [Fact]
    public async Task Register_ShouldCreateActiveDevice_AndRejectDuplicateCodeIncludingSoftDeleted()
    {
        var id = await Service().RegisterAsync(new RegisterScannerDeviceRequest(" SCN-001 ", " Cong A "));

        var detail = await Service().GetByIdAsync(id);
        detail.DeviceCode.Should().Be("SCN-001");
        detail.Label.Should().Be("Cong A");
        detail.IsActive.Should().BeTrue();

        var dup = () => Service().RegisterAsync(new RegisterScannerDeviceRequest("SCN-001", null));
        await dup.Should().ThrowAsync<ValidationException>();

        _context.ScannerDevices.Single(d => d.Id == id).IsDeleted = true;
        _context.SaveChanges();
        var deleted = () => Service().RegisterAsync(new RegisterScannerDeviceRequest("SCN-001", null));
        await deleted.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData("SCN-01", true)]
    [InlineData("a.b_c-9", true)]
    [InlineData("", false)]
    [InlineData("bad code", false)]
    [InlineData("so/slash", false)]
    public void Validator_ShouldRestrictDeviceCode(string code, bool valid) =>
        new RegisterScannerDeviceRequestValidator().Validate(new RegisterScannerDeviceRequest(code, null)).IsValid.Should().Be(valid);

    [Fact]
    public void Validator_ShouldBoundCodeLabelAndPaging()
    {
        new RegisterScannerDeviceRequestValidator().Validate(new RegisterScannerDeviceRequest(new string('a', 51), null)).IsValid.Should().BeFalse();
        new RegisterScannerDeviceRequestValidator().Validate(new RegisterScannerDeviceRequest("A", new string('x', 101))).IsValid.Should().BeFalse();
        new GetScannerDevicesRequestValidator().Validate(new GetScannerDevicesRequest(0, 10)).IsValid.Should().BeFalse();
        new GetScannerDevicesRequestValidator().Validate(new GetScannerDevicesRequest(1, 101)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ShouldChangeOnlyLabel()
    {
        var id = await Service().RegisterAsync(new RegisterScannerDeviceRequest("SCN-002", "Cu"));

        await Service().UpdateAsync(id, new UpdateScannerDeviceRequest("Moi"));

        var detail = await Service().GetByIdAsync(id);
        detail.Label.Should().Be("Moi");
        detail.DeviceCode.Should().Be("SCN-002");
    }

    [Fact]
    public async Task Deactivate_WithOpenEndedOrFutureAssignment_ShouldConflict_AndKeepAssignmentUntouched()
    {
        var id = await Service().RegisterAsync(new RegisterScannerDeviceRequest("SCN-003", null));
        var open = Assign(id, null);
        var future = Assign(id, Now.AddHours(2));

        var act = () => Service().DeactivateAsync(id);

        await act.Should().ThrowAsync<ConflictException>();
        (await Service().GetByIdAsync(id)).IsActive.Should().BeTrue();
        (await Service().GetByIdAsync(id)).ActiveAssignmentCount.Should().Be(2);
        _context.ScannerAssignments.Single(a => a.Id == open.Id).ActiveUntil.Should().BeNull();
        _context.ScannerAssignments.Single(a => a.Id == future.Id).ActiveUntil.Should().Be(Now.AddHours(2));
    }

    [Fact]
    public async Task Deactivate_AfterAssignmentsEnded_ShouldSucceed_AndPreserveHistory()
    {
        var id = await Service().RegisterAsync(new RegisterScannerDeviceRequest("SCN-004", null));
        var ended = Assign(id, Now.AddMinutes(-5)); // đã kết thúc (vd bị thu hồi)
        _context.CheckInEvents.Add(new CheckInEvent { ClientEventId = "evt-1", ScannerDeviceId = id, Outcome = "Success", SyncStatus = "Synced" });
        _context.SaveChanges();

        await Service().DeactivateAsync(id);

        var detail = await Service().GetByIdAsync(id);
        detail.IsActive.Should().BeFalse();
        detail.ActiveAssignmentCount.Should().Be(0);
        detail.CheckInEventCount.Should().Be(1);
        _context.ScannerAssignments.Single(a => a.Id == ended.Id).DeviceId.Should().Be(id);

        await Service().ActivateAsync(id);
        (await Service().GetByIdAsync(id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_AfterFutureAssignmentIsRevoked_ShouldSucceed()
    {
        var id = await Service().RegisterAsync(new RegisterScannerDeviceRequest("SCN-005", null));
        var future = Assign(id, null);
        future.ActiveFrom = Now.AddDays(3);
        _context.SaveChanges();
        (await Service().GetByIdAsync(id)).ActiveAssignmentCount.Should().Be(1);

        future.ActiveUntil = future.ActiveFrom; // cách RevokeScannerAssignment thu hồi lịch tương lai: khoảng rỗng
        _context.SaveChanges();

        (await Service().GetByIdAsync(id)).ActiveAssignmentCount.Should().Be(0);
        await Service().DeactivateAsync(id);
        (await Service().GetByIdAsync(id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task List_ShouldFilterBySearchAndActivity()
    {
        var a = await Service().RegisterAsync(new RegisterScannerDeviceRequest("GATE-A", "Cong chinh"));
        await Service().RegisterAsync(new RegisterScannerDeviceRequest("GATE-B", "Cong phu"));
        await Service().DeactivateAsync(a);

        (await Service().GetPageAsync(new GetScannerDevicesRequest(Search: "gate-a"))).Items.Should().ContainSingle();
        (await Service().GetPageAsync(new GetScannerDevicesRequest(Search: "phu"))).Items.Should().ContainSingle(d => d.DeviceCode == "GATE-B");
        (await Service().GetPageAsync(new GetScannerDevicesRequest(IsActive: false))).Items.Should().ContainSingle(d => d.DeviceCode == "GATE-A");
    }

    [Fact]
    public async Task Get_Unknown_ShouldBeNotFound()
    {
        var act = () => Service().GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void Controller_ShouldRequireAdmin()
    {
        typeof(AdminScannerDevicesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().ContainSingle(a => a.Policy == "AdminOnly");
        typeof(AdminScannerDevicesController).GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true)).Should().BeEmpty();
    }
}
