using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.Refunds;
using WaterbusSystem.Application.Features.Refunds.Services;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Exceptions;
using Xunit;

namespace WaterbusSystem.UnitTests.Refunds;

public sealed class RefundExportServiceTests
{
    private sealed class FakeRepository(IReadOnlyList<PendingManualRefund> rows) : IRefundExportRepository
    {
        public Guid? RequestedTripId { get; private set; }
        public int RequestedSkip { get; private set; }
        public int RequestedLimit { get; private set; }
        public List<(IReadOnlyCollection<Guid> Ids, Guid Admin, DateTimeOffset At)> Marks { get; } = [];

        public Task<IReadOnlyList<PendingManualRefund>> ListPendingManualAsync(Guid? tripId, int skip, int take, CancellationToken ct)
        {
            RequestedTripId = tripId;
            RequestedSkip = skip;
            RequestedLimit = take;
            return Task.FromResult<IReadOnlyList<PendingManualRefund>>(rows.Skip(skip).Take(take).ToList());
        }

        public Task<int> CountPendingManualAsync(Guid? tripId, CancellationToken ct) => Task.FromResult(rows.Count);

        public Task MarkExportedAsync(IReadOnlyCollection<Guid> refundIds, Guid adminId, DateTimeOffset exportedAt, CancellationToken ct)
        {
            Marks.Add((refundIds, adminId, exportedAt));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUser(string? id) : ICurrentUserService
    {
        public string? UserId => id;
        public string? UserRole => "Admin";
    }

    private sealed class FakeClock(DateTimeOffset now) : IDateTimeService
    {
        public DateTimeOffset Now => now;
    }

    private static PendingManualRefund Row(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "BK", Guid.NewGuid(), "An", "an@example.com", null, 1000m, "VND", ["T1"],
            "Tuyến", DateTimeOffset.UtcNow, false, "Cancelled", "REF", "NO", DateTimeOffset.UtcNow, "Processing", DateTimeOffset.UtcNow, null);

    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 3, 5, 0, TimeSpan.Zero);

    private static RefundExportService Service(FakeRepository repository, string? userId = null) =>
        new(repository, new FakeUser(userId ?? Admin.ToString()), new FakeClock(Now), NullLogger<RefundExportService>.Instance);

    [Fact]
    public async Task Export_BuildsFile_AndMarksExactlyTheExportedRefunds()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var repository = new FakeRepository([Row(a), Row(b)]);

        var file = await Service(repository).ExportAsync(new());

        file.RowCount.Should().Be(2);
        file.Content.Should().NotBeEmpty();
        file.FileName.Should().Be("hoan-tien-20261112-1005.csv");           // 03:05 UTC = 10:05 GMT+7
        var mark = repository.Marks.Should().ContainSingle().Subject;
        mark.Ids.Should().BeEquivalentTo([a, b]);
        mark.Admin.Should().Be(Admin);
        mark.At.Should().Be(Now);
    }

    [Fact]
    public async Task Export_PassesTheTripFilter_AndAsksForOneRowBeyondTheCap()
    {
        var tripId = Guid.NewGuid();
        var repository = new FakeRepository([]);

        await Service(repository).ExportAsync(new(tripId));

        repository.RequestedTripId.Should().Be(tripId);
        repository.RequestedLimit.Should().Be(RefundExportService.MaxRows + 1);
    }

    [Fact]
    public async Task Export_WithNothingPending_ReturnsAHeaderOnlyFile_AndMarksNothing()
    {
        var repository = new FakeRepository([]);

        var file = await Service(repository).ExportAsync(new());

        file.RowCount.Should().Be(0);
        file.Content.Should().NotBeEmpty();
        repository.Marks.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_AboveTheCap_IsConflict_AndMarksNothing()
    {
        var repository = new FakeRepository(Enumerable.Range(0, RefundExportService.MaxRows + 1).Select(_ => Row()).ToList());

        await Service(repository).Invoking(s => s.ExportAsync(new())).Should().ThrowAsync<ConflictException>();

        repository.Marks.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_ExactlyAtTheCap_Succeeds()
    {
        var repository = new FakeRepository(Enumerable.Range(0, RefundExportService.MaxRows).Select(_ => Row()).ToList());

        var file = await Service(repository).ExportAsync(new());

        file.RowCount.Should().Be(RefundExportService.MaxRows);
        repository.Marks.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task Export_WithoutAnIdentifiableAdmin_IsForbidden_AndNeverReadsOrMarks(string? userId)
    {
        var repository = new FakeRepository([Row()]);
        var service = new RefundExportService(repository, new FakeUser(userId), new FakeClock(Now), NullLogger<RefundExportService>.Instance);

        await service.Invoking(s => s.ExportAsync(new())).Should().ThrowAsync<ForbiddenAccessException>();

        repository.Marks.Should().BeEmpty();
    }

    [Fact]
    public async Task ListPending_IsPaged_ReportsTheTotal_AndNeverMarksAnything()
    {
        var rows = Enumerable.Range(0, 5).Select(_ => Row()).ToList();
        var repository = new FakeRepository(rows);

        var page = await Service(repository).ListPendingAsync(new(null, Page: 2, PageSize: 2));

        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);
        page.Items.Select(i => i.RefundId).Should().Equal(rows[2].RefundId, rows[3].RefundId);
        repository.RequestedSkip.Should().Be(2);
        repository.Marks.Should().BeEmpty();
    }

    [Fact]
    public async Task ListPending_PassesTheTripFilter_AndClampsThePageSize()
    {
        var tripId = Guid.NewGuid();
        var repository = new FakeRepository([Row()]);

        await Service(repository).ListPendingAsync(new(tripId, Page: 0, PageSize: 1000));

        repository.RequestedTripId.Should().Be(tripId);
        repository.RequestedSkip.Should().Be(0);
        repository.RequestedLimit.Should().Be(100);
    }
}
