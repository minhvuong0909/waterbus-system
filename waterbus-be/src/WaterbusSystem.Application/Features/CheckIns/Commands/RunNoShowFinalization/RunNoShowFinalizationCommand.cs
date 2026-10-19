using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.CheckIns.Commands.RunNoShowFinalization;

public record NoShowFinalizationResult(int StopCallsEligible, int StopCallsDeferred, int TicketsFinalized);

/// <summary>
/// Job định kỳ kết luận NoShow cho Ticket chưa check-in sau khi cửa sổ check-in của đúng bến lên đã
/// đóng (BR-BOARD-05): "NoShow chỉ được kết luận khi cơ hội check-in tại bến lên của Ticket kết thúc
/// và dữ liệu offline liên quan đã được đối soát hợp lý."
/// BR-OFF-04: không coi offline là tuyệt đối trước khi backend reconciliation - trì hoãn finalize
/// cho bất kỳ điểm dừng nào còn CheckInEvent chưa giải quyết (SyncStatus Pending/Conflict).
/// Idempotent: chỉ xét Ticket còn NotCheckedIn, chạy lại không ảnh hưởng Ticket đã finalize.
/// </summary>
public record RunNoShowFinalizationCommand : IRequest<NoShowFinalizationResult>;

public class RunNoShowFinalizationCommandHandler : IRequestHandler<RunNoShowFinalizationCommand, NoShowFinalizationResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTime;
    private readonly NoShowFinalizationOptions _options;
    private readonly ILogger<RunNoShowFinalizationCommandHandler> _logger;

    public RunNoShowFinalizationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTime,
        IOptions<NoShowFinalizationOptions> options,
        ILogger<RunNoShowFinalizationCommandHandler> logger)
    {
        _context = context;
        _dateTime = dateTime;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<NoShowFinalizationResult> Handle(RunNoShowFinalizationCommand request, CancellationToken cancellationToken)
    {
        var now = _dateTime.Now;
        var deadline = now.AddMinutes(-_options.GraceMinutes);

        // Điểm dừng đã đóng cửa sổ check-in (+ thời gian chờ đối soát) từ trước deadline.
        var closedStopCallIds = await _context.TripStopCalls.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CheckInCloseAt != null && c.CheckInCloseAt <= deadline)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (closedStopCallIds.Count == 0)
        {
            return new NoShowFinalizationResult(0, 0, 0);
        }

        // BR-OFF-04: trì hoãn các điểm dừng còn CheckInEvent chưa giải quyết (đồng bộ offline đang dở).
        var unresolvedStopCallIds = await _context.CheckInEvents.AsNoTracking()
            .Where(e => closedStopCallIds.Contains(e.TripStopCallId) &&
                        (e.SyncStatus == "Pending" || e.SyncStatus == "Conflict"))
            .Select(e => e.TripStopCallId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var eligibleStopCallIds = closedStopCallIds.Except(unresolvedStopCallIds).ToList();
        if (eligibleStopCallIds.Count == 0)
        {
            _logger.LogInformation(
                "[NOSHOW] {Closed} điểm dừng đã đóng cửa sổ check-in nhưng đều còn sự kiện offline chưa đồng bộ, chưa kết luận NoShow.",
                closedStopCallIds.Count);
            return new NoShowFinalizationResult(0, closedStopCallIds.Count, 0);
        }

        var tickets = await _context.Tickets
            .Where(t => !t.IsDeleted &&
                        t.Status == TicketStatus.Valid &&
                        t.BoardingStatus == BoardingStatus.NotCheckedIn &&
                        eligibleStopCallIds.Contains(t.SeatReservation!.Booking!.BoardingCallId))
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            ticket.BoardingStatus = BoardingStatus.NoShow;
            ticket.NoShowFinalizedAt = now;
        }

        if (tickets.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "[NOSHOW] Kết luận NoShow cho {Tickets} vé tại {StopCalls} điểm dừng; trì hoãn {Deferred} điểm dừng còn dữ liệu offline chưa đối soát.",
            tickets.Count, eligibleStopCallIds.Count, unresolvedStopCallIds.Count);

        return new NoShowFinalizationResult(eligibleStopCallIds.Count, unresolvedStopCallIds.Count, tickets.Count);
    }
}
