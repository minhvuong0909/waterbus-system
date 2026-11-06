using System.Buffers.Binary;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Scanner.Commands.ClaimPrimaryScanner;

/// <summary>Kết quả claim quyền scanner offline chính tại một TripStopCall.</summary>
public sealed record ClaimPrimaryScannerResult(
    bool Success,
    bool IsPrimaryOffline,
    long AssignmentVersion,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveUntil,
    string? Message);

/// <summary>Nhận quyền offline chính của một phân công scanner đã được thiết lập.</summary>
public sealed record ClaimPrimaryScannerCommand(
    string DeviceId,
    Guid TripStopCallId,
    string? DeviceCode,
    long? ExpectedVersion) : IRequest<ClaimPrimaryScannerResult>;

public sealed class ClaimPrimaryScannerCommandValidator : AbstractValidator<ClaimPrimaryScannerCommand>
{
    public ClaimPrimaryScannerCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().When(x => string.IsNullOrWhiteSpace(x.DeviceCode));
        RuleFor(x => x.DeviceCode).NotEmpty().MaximumLength(50).When(x => !string.IsNullOrWhiteSpace(x.DeviceCode));
        RuleFor(x => x.TripStopCallId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion.HasValue);
    }
}

public sealed class ClaimPrimaryScannerCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<ClaimPrimaryScannerCommand, ClaimPrimaryScannerResult>
{
    public async Task<ClaimPrimaryScannerResult> Handle(
        ClaimPrimaryScannerCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var staffId))
            throw new ForbiddenAccessException("Không xác định được tài khoản nhân viên từ JWT.");

        var now = DateTimeOffset.UtcNow;
        var deviceIdentifier = string.IsNullOrWhiteSpace(request.DeviceCode)
            ? request.DeviceId
            : request.DeviceCode;
        var deviceQuery = context.ScannerDevices.Where(d => d.IsActive);
        var device = Guid.TryParse(deviceIdentifier, out var deviceId)
            ? await deviceQuery.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            : await deviceQuery.FirstOrDefaultAsync(d => d.DeviceCode == deviceIdentifier, cancellationToken);

        if (device is null)
            throw new NotFoundException(nameof(ScannerDevice), deviceIdentifier);

        var assignment = await context.ScannerAssignments
            .FirstOrDefaultAsync(a => a.DeviceId == device.Id
                && a.StaffAccountId == staffId
                && a.TripStopCallId == request.TripStopCallId
                && a.ActiveFrom <= now
                && (!a.ActiveUntil.HasValue || a.ActiveUntil > now), cancellationToken)
            ?? throw new ForbiddenAccessException("Thiết bị chưa được phân công cho nhân viên tại điểm dừng này.");

        var version = ReadVersion(assignment.RowVersion);
        if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != version)
            throw new ConcurrencyException("Phân công scanner đã thay đổi. Hãy tải lại dữ liệu trước khi thử lại.");

        var inactivePrimaryAssignments = await context.ScannerAssignments
            .Where(a => a.TripStopCallId == request.TripStopCallId
                && a.Id != assignment.Id
                && a.IsPrimaryOffline
                && (a.ActiveFrom > now || (a.ActiveUntil.HasValue && a.ActiveUntil <= now)))
            .ToListAsync(cancellationToken);
        foreach (var inactiveAssignment in inactivePrimaryAssignments)
            inactiveAssignment.IsPrimaryOffline = false;

        if (inactivePrimaryAssignments.Count > 0)
            await context.SaveChangesAsync(cancellationToken);

        var activePrimaryExists = await context.ScannerAssignments.AsNoTracking()
            .AnyAsync(a => a.TripStopCallId == request.TripStopCallId
                && a.Id != assignment.Id
                && a.IsPrimaryOffline
                && a.ActiveFrom <= now
                && (!a.ActiveUntil.HasValue || a.ActiveUntil > now), cancellationToken);
        if (activePrimaryExists)
            throw new ConcurrencyException("Một thiết bị khác đang là scanner offline chính tại điểm dừng này.");

        if (!assignment.IsPrimaryOffline)
        {
            assignment.IsPrimaryOffline = true;
            await context.SaveChangesAsync(cancellationToken);
        }

        return new ClaimPrimaryScannerResult(
            true,
            assignment.IsPrimaryOffline,
            ReadVersion(assignment.RowVersion),
            assignment.ActiveFrom,
            assignment.ActiveUntil,
            "Thiết bị này được phân công soát vé chính tại bến.");
    }

    private static long ReadVersion(byte[] version) =>
        version.Length == sizeof(long) ? BinaryPrimitives.ReadInt64BigEndian(version) : 0;
}
