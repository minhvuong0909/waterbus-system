using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.CheckIns.Services;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.CheckIns.Commands.SyncCheckInEvents;

public record CheckInEventInput(
    Guid TicketId,
    Guid TripStopCallId,
    string ClientEventId,
    DateTimeOffset OccurredAtDevice,
    string? Note);

public record CheckInEventSyncResult(string ClientEventId, string Outcome, bool WasAlreadySynced, string? Error);

public record SyncCheckInEventsResultDto(IReadOnlyList<CheckInEventSyncResult> Results);

/// <summary>
/// Đồng bộ theo lô các sự kiện check-in đã ghi offline (BR-OFF-01..05). Mỗi event được xử lý và LƯU
/// ĐỘC LẬP: 1 event lỗi (vé/điểm dừng không tồn tại, v.v.) không làm mất kết quả của các event khác
/// đã đồng bộ thành công trong cùng lô. Idempotent theo ClientEventId - gửi lại lô cũ (do mất mạng
/// giữa chừng) không tạo bản ghi trùng.
/// </summary>
public record SyncCheckInEventsCommand(IReadOnlyList<CheckInEventInput> Events) : IRequest<SyncCheckInEventsResultDto>;

public class SyncCheckInEventsCommandValidator : AbstractValidator<SyncCheckInEventsCommand>
{
    private const int MaxBatchSize = 200;

    public SyncCheckInEventsCommandValidator()
    {
        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("Danh sách sự kiện check-in rỗng.")
            .Must(e => e.Count <= MaxBatchSize).WithMessage($"Mỗi lô đồng bộ tối đa {MaxBatchSize} sự kiện.");

        RuleForEach(x => x.Events).ChildRules(ev =>
        {
            ev.RuleFor(e => e.TicketId).NotEmpty().WithMessage("Thiếu mã vé.");
            ev.RuleFor(e => e.TripStopCallId).NotEmpty().WithMessage("Thiếu mã điểm dừng.");
            ev.RuleFor(e => e.ClientEventId)
                .NotEmpty().WithMessage("Thiếu định danh lần quét.")
                .MaximumLength(100).WithMessage("Định danh lần quét tối đa 100 ký tự.");
            ev.RuleFor(e => e.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
        });
    }
}

public class SyncCheckInEventsCommandHandler : IRequestHandler<SyncCheckInEventsCommand, SyncCheckInEventsResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;
    private readonly CheckInProcessor _processor;

    public SyncCheckInEventsCommandHandler(
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

    public async Task<SyncCheckInEventsResultDto> Handle(SyncCheckInEventsCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var staffId) ||
            !string.Equals(_currentUser.UserRole, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ nhân viên soát vé mới được đồng bộ check-in.");
        }

        var now = _dateTime.Now;
        var results = new List<CheckInEventSyncResult>(request.Events.Count);

        foreach (var evt in request.Events)
        {
            try
            {
                var (outcome, wasAlreadySynced) = await _processor.ProcessAsync(
                    staffId, evt.TicketId, evt.TripStopCallId, evt.ClientEventId,
                    evt.OccurredAtDevice, evt.Note, now, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                results.Add(new CheckInEventSyncResult(evt.ClientEventId, outcome.Outcome, wasAlreadySynced, null));
            }
            catch (NotFoundException ex)
            {
                // Vé/điểm dừng không tồn tại (dữ liệu offline đã cũ) - không làm fail cả lô,
                // chỉ báo lỗi cho đúng event này để client biết cần preload lại manifest.
                // Dọn entity dở dang (nếu có) để không lẫn vào lần SaveChangesAsync của event kế tiếp.
                _context.DiscardTrackedChanges();
                results.Add(new CheckInEventSyncResult(evt.ClientEventId, "Error", false, ex.Message));
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.DiscardTrackedChanges();
                results.Add(new CheckInEventSyncResult(evt.ClientEventId, "Error", false,
                    "Vé vừa được cập nhật bởi người khác. Vui lòng đồng bộ lại."));
            }
        }

        return new SyncCheckInEventsResultDto(results);
    }
}
