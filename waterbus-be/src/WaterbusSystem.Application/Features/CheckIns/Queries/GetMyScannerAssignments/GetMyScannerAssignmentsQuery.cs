using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.CheckIns.Queries.GetMyScannerAssignments;

public record MyScannerAssignmentDto(
    Guid AssignmentId,
    Guid TripStopCallId,
    Guid TripId,
    string RouteName,
    string StationName,
    int VisitOrder,
    string TripStatus,
    DateTimeOffset? EstimatedArrivalTime,
    Guid DeviceId,
    string DeviceCode,
    bool IsPrimaryOffline,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveUntil,
    DateTimeOffset? CheckInOpenAt,
    DateTimeOffset? CheckInCloseAt);

/// <summary>
/// Staff Scanner hỏi "tôi được phân công soát vé ở điểm dừng nào" để biết TripStopCallId cần
/// preload manifest / gửi check-in (BR-OFF-01, BR-BOARD-03). Chỉ trả phân công chưa kết thúc
/// (ActiveUntil null hoặc còn ở tương lai so với hiện tại), kể cả ca sắp tới.
/// </summary>
public record GetMyScannerAssignmentsQuery : IRequest<IReadOnlyList<MyScannerAssignmentDto>>;

public class GetMyScannerAssignmentsQueryHandler
    : IRequestHandler<GetMyScannerAssignmentsQuery, IReadOnlyList<MyScannerAssignmentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public GetMyScannerAssignmentsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeService dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<IReadOnlyList<MyScannerAssignmentDto>> Handle(
        GetMyScannerAssignmentsQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var staffId) ||
            !string.Equals(_currentUser.UserRole, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Chỉ nhân viên soát vé mới xem được phân công của mình.");
        }

        var now = _dateTime.Now;

        return await _context.ScannerAssignments.AsNoTracking()
            .Where(a => !a.IsDeleted && a.StaffAccountId == staffId &&
                        (a.ActiveUntil == null || a.ActiveUntil >= now))
            .OrderBy(a => a.ActiveFrom)
            .Select(a => new MyScannerAssignmentDto(
                a.Id,
                a.TripStopCallId,
                a.TripStopCall!.TripId,
                a.TripStopCall.Trip!.Route!.Name,
                a.TripStopCall.RouteStop!.Station!.Name,
                a.TripStopCall.VisitOrder,
                a.TripStopCall.Trip.Status.ToString(),
                a.TripStopCall.EstimatedArrivalTime,
                a.DeviceId,
                a.Device!.DeviceCode,
                a.IsPrimaryOffline,
                a.ActiveFrom,
                a.ActiveUntil,
                a.TripStopCall.CheckInOpenAt,
                a.TripStopCall.CheckInCloseAt))
            .ToListAsync(cancellationToken);
    }
}
