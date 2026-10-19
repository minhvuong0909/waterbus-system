using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.CheckIns.Commands.SubmitCheckIn;

public record CheckInResultDto(string Outcome, DateTimeOffset? CheckedInAt, string? TicketCode, string? PassengerName);

/// <summary>
/// Staff quét QR và check-in 1 Ticket tại đúng điểm dừng khi có mạng (BR-QR-03, BR-BOARD-01..05).
/// Logic kiểm tra/ghi nhận dùng chung với đồng bộ offline qua <see cref="CheckInProcessor"/>.
/// </summary>
public record SubmitCheckInCommand(
    Guid TicketId,
    Guid TripStopCallId,
    string ClientEventId,
    DateTimeOffset OccurredAtDevice,
    string? Note) : IRequest<CheckInResultDto>;

public class SubmitCheckInCommandValidator : AbstractValidator<SubmitCheckInCommand>
{
    public SubmitCheckInCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty().WithMessage("Thiếu mã vé.");
        RuleFor(x => x.TripStopCallId).NotEmpty().WithMessage("Thiếu mã điểm dừng.");
        RuleFor(x => x.ClientEventId)
            .NotEmpty().WithMessage("Thiếu định danh lần quét.")
            .MaximumLength(100).WithMessage("Định danh lần quét tối đa 100 ký tự.");
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class SubmitCheckInCommandHandler : IRequestHandler<SubmitCheckInCommand, CheckInResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;
    private readonly CheckInProcessor _processor;

    public SubmitCheckInCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeService dateTime,
        CheckInProcessor processor)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _processor = processor;
    }

    public async Task<CheckInResultDto> Handle(SubmitCheckInCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var staffId) ||
            !string.Equals(_currentUser.UserRole, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            // BR-ROLE-05: check-in là nghiệp vụ của Staff; không mở rộng cho Admin ở đây.
            throw new ForbiddenAccessException("Chỉ nhân viên soát vé mới được check-in.");
        }

        var now = _dateTime.Now;

        var (outcome, _) = await _processor.ProcessAsync(
            staffId, request.TicketId, request.TripStopCallId, request.ClientEventId,
            request.OccurredAtDevice, request.Note, now, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Vé vừa được cập nhật bởi người khác. Vui lòng quét lại.", ex);
        }

        return new CheckInResultDto(outcome.Outcome, outcome.CheckedInAt, outcome.TicketCode, outcome.PassengerName);
    }
}
