using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.CheckIns.Commands.RevokeScannerAssignment;

public record RevokeScannerAssignmentResultDto(Guid AssignmentId, DateTimeOffset ActiveUntil);

/// <summary>
/// Admin thu hồi (kết thúc ngay) một phân công scanner - cần dùng trước khi gán Primary Offline
/// Scanner mới cho cùng điểm dừng (BR-OFF-02). Kết thúc bằng cách hạ ActiveUntil về hiện tại thay vì
/// xoá, để giữ lịch sử phân công cho đối soát.
/// </summary>
public record RevokeScannerAssignmentCommand(Guid AssignmentId) : IRequest<RevokeScannerAssignmentResultDto>;

public class RevokeScannerAssignmentCommandValidator : AbstractValidator<RevokeScannerAssignmentCommand>
{
    public RevokeScannerAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty().WithMessage("Thiếu mã phân công.");
    }
}

public class RevokeScannerAssignmentCommandHandler : IRequestHandler<RevokeScannerAssignmentCommand, RevokeScannerAssignmentResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTime;

    public RevokeScannerAssignmentCommandHandler(IApplicationDbContext context, IDateTimeService dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<RevokeScannerAssignmentResultDto> Handle(RevokeScannerAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _context.ScannerAssignments
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId && !a.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(ScannerAssignment), request.AssignmentId);

        var now = _dateTime.Now;

        if (assignment.ActiveFrom > now)
        {
            // Chưa từng có hiệu lực (phân công cho tương lai) - thu hồi hẳn, không để lại cửa sổ ảo.
            assignment.ActiveUntil = assignment.ActiveFrom;
        }
        else if (assignment.ActiveUntil is null || assignment.ActiveUntil > now)
        {
            assignment.ActiveUntil = now;
        }
        // Đã kết thúc từ trước thì giữ nguyên (thao tác idempotent).

        assignment.UpdatedAt = now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Phân công vừa được cập nhật bởi người khác. Vui lòng tải lại.", ex);
        }

        return new RevokeScannerAssignmentResultDto(assignment.Id, assignment.ActiveUntil!.Value);
    }
}
