using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.CheckIns.Commands.AssignScanner;

public record AssignScannerResultDto(
    Guid AssignmentId,
    Guid DeviceId,
    Guid StaffAccountId,
    Guid TripStopCallId,
    bool IsPrimaryOffline,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveUntil);

/// <summary>
/// Admin phân công thiết bị quét + Staff cho một điểm dừng cụ thể (BR-CFG-01). Chặn gán 2
/// Primary Offline Scanner chồng thời gian cho cùng điểm dừng (BR-OFF-02): chỉ một thiết bị được
/// quyền ghi nhận check-in offline cho cùng phạm vi Trip + boarding point được phân công; Admin phải
/// thu hồi (RevokeScannerAssignmentCommand) phân công Primary cũ trước khi gán Primary mới.
/// </summary>
public record AssignScannerCommand(
    Guid DeviceId,
    Guid StaffAccountId,
    Guid TripStopCallId,
    bool IsPrimaryOffline,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveUntil) : IRequest<AssignScannerResultDto>;

public class AssignScannerCommandValidator : AbstractValidator<AssignScannerCommand>
{
    public AssignScannerCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().WithMessage("Thiếu mã thiết bị.");
        RuleFor(x => x.StaffAccountId).NotEmpty().WithMessage("Thiếu mã nhân viên.");
        RuleFor(x => x.TripStopCallId).NotEmpty().WithMessage("Thiếu mã điểm dừng.");
        RuleFor(x => x.ActiveUntil)
            .GreaterThan(x => x.ActiveFrom)
            .When(x => x.ActiveUntil.HasValue)
            .WithMessage("Thời điểm kết thúc phân công phải sau thời điểm bắt đầu.");
    }
}

public class AssignScannerCommandHandler : IRequestHandler<AssignScannerCommand, AssignScannerResultDto>
{
    private readonly IApplicationDbContext _context;

    public AssignScannerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AssignScannerResultDto> Handle(AssignScannerCommand request, CancellationToken cancellationToken)
    {
        var device = await _context.ScannerDevices
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && !d.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(ScannerDevice), request.DeviceId);

        if (!device.IsActive)
        {
            throw new ConflictException("Thiết bị đã bị ngưng hoạt động, không thể phân công.");
        }

        var stopCall = await _context.TripStopCalls
            .FirstOrDefaultAsync(c => c.Id == request.TripStopCallId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(TripStopCall), request.TripStopCallId);

        if (request.IsPrimaryOffline)
        {
            var requestedEnd = request.ActiveUntil ?? DateTimeOffset.MaxValue;

            // BR-OFF-02: chỉ 1 Primary Offline Scanner hoạt động cùng lúc cho cùng điểm dừng.
            var hasOverlappingPrimary = await _context.ScannerAssignments.AsNoTracking().AnyAsync(a =>
                    !a.IsDeleted &&
                    a.TripStopCallId == request.TripStopCallId &&
                    a.IsPrimaryOffline &&
                    a.ActiveFrom < requestedEnd &&
                    (a.ActiveUntil ?? DateTimeOffset.MaxValue) > request.ActiveFrom,
                cancellationToken);

            if (hasOverlappingPrimary)
            {
                throw new ConflictException(
                    "Điểm dừng này đã có Primary Offline Scanner khác đang hoạt động trong khoảng thời gian này. " +
                    "Vui lòng thu hồi phân công cũ trước.");
            }
        }

        var assignment = new ScannerAssignment
        {
            DeviceId = device.Id,
            Device = device,
            StaffAccountId = request.StaffAccountId,
            TripStopCallId = stopCall.Id,
            TripStopCall = stopCall,
            IsPrimaryOffline = request.IsPrimaryOffline,
            ActiveFrom = request.ActiveFrom,
            ActiveUntil = request.ActiveUntil
        };
        _context.ScannerAssignments.Add(assignment);

        await _context.SaveChangesAsync(cancellationToken);

        return new AssignScannerResultDto(
            assignment.Id, assignment.DeviceId, assignment.StaffAccountId, assignment.TripStopCallId,
            assignment.IsPrimaryOffline, assignment.ActiveFrom, assignment.ActiveUntil);
    }
}
