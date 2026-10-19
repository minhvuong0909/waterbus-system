using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.CheckIns.Commands.SetCheckInWindow;

public record SetCheckInWindowResultDto(Guid TripStopCallId, DateTimeOffset CheckInOpenAt, DateTimeOffset CheckInCloseAt);

/// <summary>
/// Admin cấu hình cửa sổ check-in cho một điểm dừng cụ thể (BR-BOARD-05: cửa sổ check-in xác định
/// theo bến lên/điểm dừng thực sự được phục vụ, không phải một mốc chung cho cả Trip; BR-CFG-01:
/// Admin quản lý cửa sổ check-in).
/// </summary>
public record SetCheckInWindowCommand(
    Guid TripStopCallId,
    DateTimeOffset CheckInOpenAt,
    DateTimeOffset CheckInCloseAt) : IRequest<SetCheckInWindowResultDto>;

public class SetCheckInWindowCommandValidator : AbstractValidator<SetCheckInWindowCommand>
{
    public SetCheckInWindowCommandValidator()
    {
        RuleFor(x => x.TripStopCallId).NotEmpty().WithMessage("Thiếu mã điểm dừng.");
        RuleFor(x => x.CheckInCloseAt)
            .GreaterThan(x => x.CheckInOpenAt)
            .WithMessage("Giờ đóng cửa sổ check-in phải sau giờ mở.");
    }
}

public class SetCheckInWindowCommandHandler : IRequestHandler<SetCheckInWindowCommand, SetCheckInWindowResultDto>
{
    private readonly IApplicationDbContext _context;

    public SetCheckInWindowCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SetCheckInWindowResultDto> Handle(SetCheckInWindowCommand request, CancellationToken cancellationToken)
    {
        var stopCall = await _context.TripStopCalls
            .FirstOrDefaultAsync(c => c.Id == request.TripStopCallId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(TripStopCall), request.TripStopCallId);

        // Điểm dừng đã khởi hành/bị bỏ qua thì không còn ý nghĩa để cấu hình cửa sổ check-in.
        if (stopCall.Status is "Departed" or "Skipped")
        {
            throw new ConflictException("Không thể đặt cửa sổ check-in cho điểm dừng đã khởi hành hoặc bị bỏ qua.");
        }

        stopCall.CheckInOpenAt = request.CheckInOpenAt;
        stopCall.CheckInCloseAt = request.CheckInCloseAt;
        stopCall.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Điểm dừng vừa được cập nhật bởi người khác. Vui lòng tải lại.", ex);
        }

        return new SetCheckInWindowResultDto(stopCall.Id, request.CheckInOpenAt, request.CheckInCloseAt);
    }
}
