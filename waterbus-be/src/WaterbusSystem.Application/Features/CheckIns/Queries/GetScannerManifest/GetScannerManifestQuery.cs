using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.CheckIns.Queries.GetScannerManifest;

public record ManifestTicketDto(
    Guid TicketId,
    string TicketCode,
    string PassengerName,
    string SeatCode,
    string SeatClass,
    string BoardingStatus,
    decimal PaidFare);

/// <summary>
/// PublicBookingId là khoá tra cứu mà Staff Scanner dùng ngay khi quét QR (BR-QR-02) - bắt buộc
/// phải có trong manifest, không chỉ BookingCode (mã hiển thị, không dùng để tra cứu).
/// </summary>
public record ManifestBookingDto(
    Guid BookingId,
    string PublicBookingId,
    string BookingCode,
    Guid TripId,
    string CustomerName,
    string? CustomerPhone,
    decimal QuotedTotal,
    int QrCredentialVersion,
    string Status,
    IReadOnlyList<ManifestTicketDto> Tickets);

public record ManifestTripDto(
    Guid TripId,
    string RouteName,
    string BoatName,
    string DepartureStationName,
    string ArrivalStationName,
    DateTimeOffset DepartureTime,
    DateTimeOffset? ArrivalTime,
    DateTimeOffset? SalesCloseAt,
    string TripType,
    string Status);

public record ScannerManifestDto(
    Guid TripStopCallId,
    string StationName,
    DateTimeOffset? CheckInOpenAt,
    DateTimeOffset? CheckInCloseAt,
    ManifestTripDto Trip,
    IReadOnlyList<ManifestBookingDto> Bookings,
    DateTimeOffset GeneratedAt);

/// <summary>
/// Staff Scanner preload manifest trước giờ mở cổng (BR-OFF-01): toàn bộ Booking/Ticket hợp lệ có
/// bến lên đúng điểm dừng được phân công, kèm cửa sổ check-in và thông tin Trip để hiển thị, phục vụ
/// làm việc offline khi mất mạng.
/// </summary>
public record GetScannerManifestQuery(Guid TripStopCallId) : IRequest<ScannerManifestDto>;

public class GetScannerManifestQueryValidator : AbstractValidator<GetScannerManifestQuery>
{
    public GetScannerManifestQueryValidator()
    {
        RuleFor(x => x.TripStopCallId).NotEmpty().WithMessage("Thiếu mã điểm dừng.");
    }
}

public class GetScannerManifestQueryHandler : IRequestHandler<GetScannerManifestQuery, ScannerManifestDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public GetScannerManifestQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeService dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ScannerManifestDto> Handle(GetScannerManifestQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var staffId) ||
            !string.Equals(_currentUser.UserRole, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ nhân viên soát vé mới được tải manifest.");
        }

        var now = _dateTime.Now;

        var stopCall = await _context.TripStopCalls.AsNoTracking()
            .Include(c => c.RouteStop!).ThenInclude(rs => rs!.Station)
            .Include(c => c.Trip!).ThenInclude(t => t!.Route!).ThenInclude(r => r!.DepartureStation)
            .Include(c => c.Trip!).ThenInclude(t => t!.Route!).ThenInclude(r => r!.ArrivalStation)
            .Include(c => c.Trip!).ThenInclude(t => t!.Boat)
            .FirstOrDefaultAsync(c => c.Id == request.TripStopCallId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(TripStopCall), request.TripStopCallId);

        // BR-BOARD-03: không trả manifest cho điểm dừng mà Staff không được phân công.
        var hasAssignment = await _context.ScannerAssignments.AsNoTracking().AnyAsync(a =>
                a.StaffAccountId == staffId &&
                a.TripStopCallId == request.TripStopCallId &&
                a.ActiveFrom <= now &&
                (a.ActiveUntil == null || a.ActiveUntil >= now),
            cancellationToken);
        if (!hasAssignment)
        {
            throw new ForbiddenAccessException("Bạn không được phân công cho điểm dừng này.");
        }

        var trip = stopCall.Trip ?? throw new NotFoundException(nameof(Trip), stopCall.TripId);
        var route = trip.Route ?? throw new NotFoundException(nameof(Domain.Entities.Route), trip.RouteId);
        var station = stopCall.RouteStop?.Station
            ?? throw new NotFoundException(nameof(Station), stopCall.RouteStopId);

        var tripDto = new ManifestTripDto(
            trip.Id,
            route.Name,
            trip.Boat?.Name ?? "",
            route.DepartureStation?.Name ?? "",
            route.ArrivalStation?.Name ?? "",
            trip.DepartureTime,
            trip.ArrivalTime,
            trip.SalesCloseAt,
            trip.TripType.ToString(),
            trip.Status.ToString());

        // Chỉ các Ticket có bến lên (BoardingCallId) đúng điểm dừng này mới thuộc manifest của bến này
        // (BR-BOARD-03: Staff không check-in Ticket của bến khác dù khách đứng cùng khu vực).
        var tickets = await _context.Tickets.AsNoTracking()
            .Include(t => t.SeatReservation!).ThenInclude(r => r!.Booking)
            .Where(t => !t.IsDeleted && t.Status == TicketStatus.Valid &&
                        t.SeatReservation!.Booking!.BoardingCallId == request.TripStopCallId)
            .ToListAsync(cancellationToken);

        var bookings = tickets
            .GroupBy(t => t.SeatReservation!.BookingId)
            .Select(g =>
            {
                var booking = g.First().SeatReservation!.Booking!;
                return new ManifestBookingDto(
                    booking.Id,
                    booking.PublicBookingId,
                    booking.BookingCode,
                    booking.TripId,
                    booking.CustomerName,
                    booking.CustomerPhone,
                    booking.QuotedTotal,
                    booking.QrCredentialVersion,
                    booking.Status.ToString(),
                    g.Select(t => new ManifestTicketDto(
                        t.Id,
                        t.TicketCode,
                        t.SeatReservation!.PassengerName,
                        t.SeatReservation.SeatCodeAtSale ?? "",
                        t.SeatReservation.SeatClassAtSale ?? "",
                        t.BoardingStatus.ToString(),
                        t.PaidFareSnapshot))
                        .ToList());
            })
            .ToList();

        return new ScannerManifestDto(
            stopCall.Id, station.Name, stopCall.CheckInOpenAt, stopCall.CheckInCloseAt, tripDto, bookings, now);
    }
}
