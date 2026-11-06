using MediatR;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.Features.Scanner.Queries.GetScannerManifest;

/// <summary>Thông tin khóa công khai dùng xác minh QR offline.</summary>
public sealed record ScannerPublicKeyDto(string Kid, string Algorithm, string PublicKeyPem, bool IsActive);

/// <summary>Thông tin chuyến được phân công cho scanner tại bến.</summary>
public sealed record ScannerTripDto(
    string Id,
    string TripCode,
    string RouteName,
    string TripType,
    string BoatName,
    string DepartureStationId,
    string DepartureStationName,
    string ArrivalStationId,
    string ArrivalStationName,
    DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime,
    DateTimeOffset? SalesCloseAt,
    string Status);

/// <summary>Thông tin vé thuộc Booking cần phục vụ tại bến được phân công.</summary>
public sealed record ScannerTicketDto(
    Guid Id,
    Guid BookingId,
    string TicketNumber,
    string SeatCode,
    string SeatClass,
    string PassengerName,
    string Status,
    string BoardingStatus,
    DateTimeOffset? CheckedInAt,
    Guid? CheckedInByStaffId,
    decimal FaceFare,
    decimal PaidFare);

/// <summary>Thông tin Booking và các Ticket cần preload xuống scanner.</summary>
public sealed record ScannerBookingDto(
    Guid Id,
    string PublicBookingId,
    string BookingCode,
    Guid TripId,
    Guid TripStopCallId,
    string CustomerName,
    string? CustomerPhone,
    decimal TotalAmount,
    int QrCredentialVersion,
    string Status,
    IReadOnlyList<ScannerTicketDto> Tickets);

/// <summary>Manifest chỉ chứa các chuyến và Booking thuộc bến được phân công.</summary>
public sealed record ScannerManifestDto(
    string StationId,
    string StationName,
    DateTimeOffset GeneratedAt,
    ScannerPublicKeyDto PublicKey,
    IReadOnlyList<ScannerTripDto> Trips,
    IReadOnlyList<ScannerBookingDto> Bookings);

/// <summary>Tải manifest cho bến mà nhân viên hiện tại được phân công.</summary>
public sealed record GetScannerManifestQuery(string StationId) : IRequest<ScannerManifestDto>;

public sealed class GetScannerManifestQueryValidator : AbstractValidator<GetScannerManifestQuery>
{
    public GetScannerManifestQueryValidator()
    {
        RuleFor(x => x.StationId).NotEmpty().MaximumLength(50);
    }
}

public sealed class GetScannerManifestQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IBookingQrSigner signer) : IRequestHandler<GetScannerManifestQuery, ScannerManifestDto>
{
    public async Task<ScannerManifestDto> Handle(GetScannerManifestQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var staffId))
            throw new ForbiddenAccessException("Không xác định được tài khoản nhân viên từ JWT.");

        var station = await context.Stations.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == request.StationId && s.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(Station), request.StationId);

        var now = DateTimeOffset.UtcNow;
        var assignedCallIds = await context.ScannerAssignments.AsNoTracking()
            .Where(a => a.StaffAccountId == staffId
                && a.Device != null && a.Device.IsActive
                && (!a.ActiveUntil.HasValue || a.ActiveUntil > now)
                && a.TripStopCall != null
                && a.TripStopCall.RouteStop != null
                && a.TripStopCall.RouteStop.StationId == station.Id
                && a.TripStopCall.Status != "Skipped")
            .Select(a => a.TripStopCallId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var calls = await context.TripStopCalls.AsNoTracking()
            .Where(c => assignedCallIds.Contains(c.Id) && c.Status != "Skipped")
            .Include(c => c.RouteStop).ThenInclude(rs => rs!.Station)
            .ToListAsync(cancellationToken);
        var tripIds = calls.Select(c => c.TripId).Distinct().ToArray();

        var trips = await context.Trips.AsNoTracking()
            .Where(t => tripIds.Contains(t.Id)
                && (t.Status == TripStatus.Scheduled
                    || t.Status == TripStatus.Boarding
                    || t.Status == TripStatus.EnRoute
                    || t.Status == TripStatus.Suspended))
            .Include(t => t.Route)
            .Include(t => t.Boat)
            .ToListAsync(cancellationToken);

        var allCalls = tripIds.Length == 0
            ? []
            : await context.TripStopCalls.AsNoTracking()
                .Where(c => tripIds.Contains(c.TripId) && c.Status != "Skipped")
                .Include(c => c.RouteStop).ThenInclude(rs => rs!.Station)
                .OrderBy(c => c.VisitOrder)
                .ToListAsync(cancellationToken);

        var bookings = await context.Bookings.AsNoTracking()
            .Where(b => assignedCallIds.Contains(b.BoardingCallId)
                && b.Status == BookingStatus.Confirmed)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.SeatReservation)
            .ToListAsync(cancellationToken);

        var tripDtos = trips.Select(trip =>
        {
            var stops = allCalls.Where(c => c.TripId == trip.Id).OrderBy(c => c.VisitOrder).ToList();
            var departure = stops.FirstOrDefault()?.RouteStop?.Station;
            var arrival = stops.LastOrDefault()?.RouteStop?.Station;
            return new ScannerTripDto(
                trip.Id.ToString(),
                trip.Id.ToString("N"),
                trip.Route?.Name ?? string.Empty,
                trip.TripType.ToString(),
                trip.Boat?.Name ?? string.Empty,
                departure?.Code ?? string.Empty,
                departure?.Name ?? string.Empty,
                arrival?.Code ?? string.Empty,
                arrival?.Name ?? string.Empty,
                trip.DepartureTime,
                trip.ArrivalTime,
                trip.SalesCloseAt,
                trip.Status.ToString());
        }).ToList();

        var bookingDtos = bookings
            .Where(b => trips.Any(t => t.Id == b.TripId))
            .Select(b => new ScannerBookingDto(
                b.Id,
                b.PublicBookingId,
                b.BookingCode,
                b.TripId,
                b.BoardingCallId,
                b.CustomerName,
                b.CustomerPhone,
                b.PaidAllocation ?? b.QuotedTotal,
                b.QrCredentialVersion,
                b.Status.ToString(),
                b.Tickets
                    .Where(t => t.Status == TicketStatus.Valid && t.BoardingStatus != BoardingStatus.NoShow)
                    .Select(t => new ScannerTicketDto(
                        t.Id,
                        t.BookingId,
                        t.TicketCode,
                        t.SeatReservation?.SeatCodeAtSale ?? string.Empty,
                        t.SeatReservation?.SeatClassAtSale ?? string.Empty,
                        t.SeatReservation?.PassengerName ?? string.Empty,
                        t.Status.ToString(),
                        t.BoardingStatus.ToString(),
                        t.CheckedInAt,
                        t.CheckedInByStaffId,
                        t.FaceFareSnapshot,
                        t.PaidFareSnapshot))
                    .ToList()))
            .ToList();

        var publicKey = signer.GetPublicKey();
        return new ScannerManifestDto(
            station.Code,
            station.Name,
            now,
            new ScannerPublicKeyDto(publicKey.Kid, publicKey.Algorithm, publicKey.PublicKeyPem, true),
            tripDtos,
            bookingDtos);
    }
}
