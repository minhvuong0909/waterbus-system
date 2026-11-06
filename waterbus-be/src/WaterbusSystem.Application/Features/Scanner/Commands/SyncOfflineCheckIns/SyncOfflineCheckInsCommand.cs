using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Scanner.Commands.SyncOfflineCheckIns;

/// <summary>Một lượt check-in đã ghi nhận offline trên thiết bị Staff.</summary>
public sealed record OfflineCheckInEventDto(
    string ClientEventId,
    Guid TicketId,
    Guid TripId,
    Guid TripStopCallId,
    DateTimeOffset OccurredAtDevice,
    string? Notes);

/// <summary>Payload body gửi lên khi đồng bộ một lô check-in offline.</summary>
public sealed record SyncOfflineCheckInsRequest(
    string ScannerDeviceId,
    DateTimeOffset BatchTimestamp,
    IReadOnlyList<OfflineCheckInEventDto> Events);

/// <summary>Lô sự kiện check-in đồng bộ từ một scanner.</summary>
public sealed record SyncOfflineCheckInsCommand(
    string ScannerDeviceId,
    DateTimeOffset BatchTimestamp,
    IReadOnlyList<OfflineCheckInEventDto> Events,
    string IdempotencyKey) : IRequest<SyncOfflineCheckInsResult>;

/// <summary>Kết quả xử lý từng sự kiện trong lô đồng bộ.</summary>
public sealed record ProcessedCheckInEventDto(
    string ClientEventId,
    string Outcome,
    string SyncStatus,
    string? Message);

/// <summary>Phản hồi tổng hợp kết quả đồng bộ offline.</summary>
public sealed record SyncOfflineCheckInsResult(
    string BatchId,
    int SuccessCount,
    int ConflictCount,
    IReadOnlyList<ProcessedCheckInEventDto> ProcessedEvents);

public sealed class SyncOfflineCheckInsCommandValidator : AbstractValidator<SyncOfflineCheckInsCommand>
{
    public SyncOfflineCheckInsCommandValidator()
    {
        RuleFor(x => x.ScannerDeviceId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Events).NotNull().NotEmpty().Must(events => events.Count <= 50)
            .WithMessage("Mỗi lô đồng bộ tối đa 50 sự kiện.");
        RuleForEach(x => x.Events).ChildRules(eventRules =>
        {
            eventRules.RuleFor(e => e.ClientEventId).NotEmpty().MaximumLength(100);
            eventRules.RuleFor(e => e.TicketId).NotEmpty();
            eventRules.RuleFor(e => e.TripId).NotEmpty();
            eventRules.RuleFor(e => e.TripStopCallId).NotEmpty();
            eventRules.RuleFor(e => e.OccurredAtDevice).NotEmpty();
            eventRules.RuleFor(e => e.Notes).MaximumLength(500);
        });
    }
}

public sealed class SyncOfflineCheckInsCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<SyncOfflineCheckInsCommand, SyncOfflineCheckInsResult>
{
    public async Task<SyncOfflineCheckInsResult> Handle(
        SyncOfflineCheckInsCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var staffId))
            throw new ForbiddenAccessException("Không xác định được tài khoản nhân viên từ JWT.");

        var device = Guid.TryParse(request.ScannerDeviceId, out var deviceId)
            ? await context.ScannerDevices.FirstOrDefaultAsync(d => d.Id == deviceId && d.IsActive, cancellationToken)
            : await context.ScannerDevices.FirstOrDefaultAsync(
                d => d.DeviceCode == request.ScannerDeviceId && d.IsActive, cancellationToken);

        if (device is null)
            throw new NotFoundException(nameof(ScannerDevice), request.ScannerDeviceId);

        var now = DateTimeOffset.UtcNow;
        var results = new List<ProcessedCheckInEventDto>(request.Events.Count);
        foreach (var item in request.Events)
        {
            var previousEvent = await context.CheckInEvents.AsNoTracking()
                .FirstOrDefaultAsync(e => e.ClientEventId == item.ClientEventId, cancellationToken);
            if (previousEvent is not null)
            {
                results.Add(new ProcessedCheckInEventDto(
                    item.ClientEventId, previousEvent.Outcome, previousEvent.SyncStatus, previousEvent.Notes));
                continue;
            }

            var ticket = await context.Tickets
                .Include(t => t.Booking)
                .Include(t => t.SeatReservation)
                .FirstOrDefaultAsync(t => t.Id == item.TicketId, cancellationToken);
            var call = await context.TripStopCalls
                .FirstOrDefaultAsync(c => c.Id == item.TripStopCallId, cancellationToken);

            var (outcome, message) = await EvaluateEventAsync(
                item, ticket, call, device.Id, staffId, now, cancellationToken);

            if (ticket is not null && call is not null)
            {
                if (outcome == "Success")
                {
                    ticket.BoardingStatus = BoardingStatus.CheckedIn;
                    ticket.CheckedInAt = item.OccurredAtDevice;
                    ticket.CheckedInByStaffId = staffId;
                }

                context.CheckInEvents.Add(new CheckInEvent
                {
                    TicketId = ticket.Id,
                    StaffAccountId = staffId,
                    ScannerDeviceId = device.Id,
                    TripStopCallId = call.Id,
                    ClientEventId = item.ClientEventId,
                    OccurredAtDevice = item.OccurredAtDevice,
                    ReceivedAtServer = now,
                    Outcome = outcome,
                    SyncStatus = outcome == "Success" ? "Synced" : "Conflict",
                    Notes = message ?? item.Notes
                });
                await context.SaveChangesAsync(cancellationToken);
            }

            results.Add(new ProcessedCheckInEventDto(
                item.ClientEventId,
                outcome,
                outcome == "Success" ? "Synced" : "Conflict",
                message));
        }

        return new SyncOfflineCheckInsResult(
            request.IdempotencyKey,
            results.Count(r => r.Outcome == "Success"),
            results.Count(r => r.Outcome != "Success"),
            results);
    }

    private async Task<(string Outcome, string? Message)> EvaluateEventAsync(
        OfflineCheckInEventDto item,
        Ticket? ticket,
        TripStopCall? call,
        Guid deviceId,
        Guid staffId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (ticket is null || call is null)
            return ("TicketInvalid", "Không tìm thấy Ticket hoặc TripStopCall hợp lệ.");

        var assignment = await context.ScannerAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.DeviceId == deviceId
                && a.StaffAccountId == staffId
                && a.TripStopCallId == call.Id
                && a.IsPrimaryOffline
                && a.ActiveFrom <= item.OccurredAtDevice
                && (!a.ActiveUntil.HasValue || a.ActiveUntil >= item.OccurredAtDevice), cancellationToken);
        if (assignment is null)
            return ("Conflict", "Thiết bị không phải scanner offline chính được phân công cho điểm dừng.");

        if (ticket.Booking is null
            || ticket.Booking.TripId != item.TripId
            || call.TripId != item.TripId
            || ticket.Booking.BoardingCallId != call.Id
            || ticket.SeatReservation?.TripId != item.TripId)
            return ("InvalidStop", "Ticket không lên tại điểm dừng thuộc chuyến này.");

        if (ticket.Status != TicketStatus.Valid || ticket.Booking.Status != BookingStatus.Confirmed)
            return ("TicketInvalid", "Booking hoặc Ticket không còn hiệu lực.");

        if (ticket.BoardingStatus == BoardingStatus.CheckedIn)
            return ("AlreadyCheckedIn", "Ticket đã được check-in trước đó.");

        if (ticket.BoardingStatus == BoardingStatus.NoShow)
            return ("TicketInvalid", "Ticket đã được chốt NoShow.");

        if (!call.CheckInOpenAt.HasValue || !call.CheckInCloseAt.HasValue
            || item.OccurredAtDevice < call.CheckInOpenAt.Value
            || item.OccurredAtDevice > call.CheckInCloseAt.Value
            || item.OccurredAtDevice > now.AddMinutes(1))
            return ("Conflict", "Sự kiện nằm ngoài cửa sổ check-in của điểm dừng.");

        return ("Success", null);
    }
}
