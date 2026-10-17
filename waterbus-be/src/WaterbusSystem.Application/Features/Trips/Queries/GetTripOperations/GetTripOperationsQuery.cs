using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Rules;

namespace WaterbusSystem.Application.Features.Trips.Queries.GetTripOperations;

public record TripOperationItemDto(
    Guid Id,
    Guid? ScheduleId,
    Guid RouteId,
    string RouteCode,
    string RouteName,
    Guid? BoatId,
    string? BoatCode,
    string TripType,
    string Status,
    DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime,
    DateTimeOffset? ActualDepartureTime,
    DateTimeOffset? ActualArrivalTime,
    bool IsDelayed,
    IReadOnlyList<string> AllowedOperations);

/// <summary>
/// Chuyến cho màn hình vận hành (Admin: mọi chuyến; thuyền trưởng: chỉ chuyến của tàu mình), theo giờ khởi hành tăng dần.
/// Mặc định từ 12 giờ trước tới 7 ngày sau; tối đa 31 ngày và 200 chuyến mỗi lần.
/// </summary>
public record GetTripOperationsQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    IReadOnlyList<TripStatus>? Statuses = null) : IRequest<List<TripOperationItemDto>>;

public class GetTripOperationsQueryValidator : AbstractValidator<GetTripOperationsQuery>
{
    public const int MaxRangeDays = 31;

    public GetTripOperationsQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.To > x.From)
            .WithName("To").WithMessage("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.To - x.From <= TimeSpan.FromDays(MaxRangeDays))
            .WithName("To").WithMessage($"Chỉ được xem tối đa {MaxRangeDays} ngày mỗi lần.");
    }
}

public class GetTripOperationsQueryHandler : IRequestHandler<GetTripOperationsQuery, List<TripOperationItemDto>>
{
    private const int MaxItems = 200;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public GetTripOperationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, IDateTimeService dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<List<TripOperationItemDto>> Handle(GetTripOperationsQuery request, CancellationToken cancellationToken)
    {
        var isAdmin = string.Equals(_currentUser.UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        var isCaptain = string.Equals(_currentUser.UserRole, "Captain", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !(isCaptain && Guid.TryParse(_currentUser.UserId, out _)))
        {
            throw new ForbiddenAccessException("Chỉ Admin hoặc thuyền trưởng mới được xem danh sách chuyến vận hành.");
        }

        var now = _dateTime.Now;
        var from = request.From ?? now.AddHours(-12);
        var to = request.To ?? from.AddDays(7);

        var query = _context.Trips.AsNoTracking()
            .Where(t => !t.IsDeleted && t.DepartureTime >= from && t.DepartureTime < to);

        if (!isAdmin)
        {
            var captainId = Guid.Parse(_currentUser.UserId!);
            query = query.Where(t => t.Boat != null && t.Boat.CaptainUserId == captainId);
        }
        if (request.Statuses is { Count: > 0 })
        {
            var statuses = request.Statuses;
            query = query.Where(t => statuses.Contains(t.Status));
        }

        var rows = await query
            .OrderBy(t => t.DepartureTime)
            .Take(MaxItems)
            .Select(t => new
            {
                Trip = t,
                RouteCode = t.Route!.Code,
                RouteName = t.Route.Name,
                BoatCode = t.Boat != null ? t.Boat.Code : null
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TripOperationItemDto(
                r.Trip.Id, r.Trip.ScheduleId, r.Trip.RouteId, r.RouteCode, r.RouteName, r.Trip.BoatId, r.BoatCode,
                r.Trip.TripType.ToString(), r.Trip.Status.ToString(), r.Trip.DepartureTime, r.Trip.ArrivalTime,
                r.Trip.ActualDepartureTime, r.Trip.ActualArrivalTime, r.Trip.IsDelayed,
                TripLifecycle.AllowedOperations(r.Trip).Select(o => o.ToString()).ToList()))
            .ToList();
    }
}
