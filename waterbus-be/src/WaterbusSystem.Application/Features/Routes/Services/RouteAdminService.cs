using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Routes;
using WaterbusSystem.Application.DTOs.Responses.Routes;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Routes.Services;

/// <summary>
/// Quản lý Route/RouteStop (BR-ROUTE-01..07, BR-CFG-01). Cho phép bến lặp không liền kề (tuyến vòng). Bến/loại dịch vụ chỉ đổi được
/// khi chưa có Schedule/Trip tham chiếu (không viết lại lịch sử, BR-CFG-02); đổi bằng cách tái dùng dòng RouteStop theo SequenceNo,
/// dòng dư chỉ xoá mềm - không Remove().
/// </summary>
public sealed class RouteAdminService(
    IRouteAdminRepository routes, IUnitOfWork unitOfWork, ICommercialTransactionRunner transactions) : IRouteAdminService
{
    public Task<PaginatedList<RouteListItemResponse>> GetPageAsync(GetRoutesRequest request, CancellationToken cancellationToken = default) =>
        routes.GetPageAsync(request, cancellationToken);

    public Task<RouteDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        routes.GetDetailAsync(id, cancellationToken);

    public Task<Guid> CreateAsync(CreateRouteRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var code = request.Code.Trim();
            if (await routes.ExistsByCodeAsync(code, cancellationToken))
                throw new ValidationException([new("code", $"Mã tuyến '{code}' đã tồn tại.")]);
            await EnsureStopsValidAsync(request.Stops, request.ServiceType, cancellationToken);

            var route = new Route
            {
                Code = code, Name = request.Name.Trim(), ServiceType = request.ServiceType,
                EstimatedDurationMinutes = request.EstimatedDurationMinutes, DistanceKm = request.DistanceKm, IsActive = true,
                DepartureStationId = request.Stops[0].StationId, ArrivalStationId = request.Stops[^1].StationId
            };
            routes.Add(route);
            for (var i = 0; i < request.Stops.Count; i++)
                routes.AddStop(new RouteStop
                {
                    RouteId = route.Id, StationId = request.Stops[i].StationId, SequenceNo = i + 1,
                    CanBoard = request.Stops[i].CanBoard, CanAlight = request.Stops[i].CanAlight
                });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return route.Id;
        }, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateRouteRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var route = await routes.GetTrackedWithStopsAsync(id, cancellationToken);
            if (route.ServiceType != request.ServiceType && await routes.IsReferencedAsync(id, cancellationToken))
                throw new ConflictException("Route đã có Schedule/Trip nên không đổi được loại dịch vụ.");
            if (request.ServiceType != "Sightseeing")
            {
                var live = route.Stops.Where(s => !s.IsDeleted).OrderBy(s => s.SequenceNo).ToList();
                for (var i = 1; i < live.Count; i++)
                    if (live[i].StationId == live[i - 1].StationId)
                        throw new ValidationException([new("serviceType", "Route có hai điểm dừng liền kề cùng bến, chỉ tuyến Sightseeing được phép.")]);
            }
            route.Name = request.Name.Trim();
            route.ServiceType = request.ServiceType;
            route.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
            route.DistanceKm = request.DistanceKm;
            route.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task<RouteDetailResponse> ReplaceStopsAsync(Guid id, ReplaceRouteStopsRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var route = await routes.GetTrackedWithStopsAsync(id, cancellationToken);
            if (await routes.IsReferencedAsync(id, cancellationToken))
                throw new ConflictException("Route đã có Schedule/Trip nên không đổi được danh sách điểm dừng. Hãy tạo Route mới.");
            await EnsureStopsValidAsync(request.Stops, route.ServiceType, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < request.Stops.Count; i++)
            {
                var input = request.Stops[i];
                var row = route.Stops.FirstOrDefault(s => s.SequenceNo == i + 1);
                if (row is null)
                {
                    routes.AddStop(new RouteStop
                    {
                        RouteId = route.Id, StationId = input.StationId, SequenceNo = i + 1, CanBoard = input.CanBoard, CanAlight = input.CanAlight
                    });
                    continue;
                }
                row.StationId = input.StationId;
                row.CanBoard = input.CanBoard;
                row.CanAlight = input.CanAlight;
                row.IsDeleted = false;
                row.UpdatedAt = now;
            }
            foreach (var extra in route.Stops.Where(s => s.SequenceNo > request.Stops.Count && !s.IsDeleted))
            {
                extra.IsDeleted = true;
                extra.UpdatedAt = now;
            }
            route.DepartureStationId = request.Stops[0].StationId;
            route.ArrivalStationId = request.Stops[^1].StationId;
            route.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await routes.GetDetailAsync(id, cancellationToken);
        }, cancellationToken);

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var route = await routes.GetTrackedWithStopsAsync(id, cancellationToken);
            if (!route.IsActive) return true;
            if (await routes.HasLiveDependentsAsync(id, cancellationToken))
                throw new ConflictException("Route còn Schedule đang hoạt động hoặc Trip chưa kết thúc nên chưa thể ngừng.");
            route.IsActive = false;
            route.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var route = await routes.GetTrackedWithStopsAsync(id, cancellationToken);
        if (route.IsActive) return;
        route.IsActive = true;
        route.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Bến liền kề lặp lại chỉ cấm với Regular; Sightseeing được phép khởi hành và trở về cùng một bến (BR-ROUTE-06 - lên và xuống
    /// vẫn là hai lần xuất hiện khác nhau trong hành trình).
    /// </summary>
    private async Task EnsureStopsValidAsync(List<RouteStopInput> stops, string serviceType, CancellationToken cancellationToken)
    {
        var errors = new List<FluentValidation.Results.ValidationFailure>();
        var stations = (await routes.GetActiveStationsAsync(stops.Select(s => s.StationId).Distinct().ToList(), cancellationToken))
            .Select(s => s.Id).ToHashSet();
        for (var i = 0; i < stops.Count; i++)
        {
            if (!stations.Contains(stops[i].StationId))
                errors.Add(new($"stops[{i}].stationId", "Bến không tồn tại hoặc đã ngừng hoạt động."));
            if (serviceType != "Sightseeing" && i > 0 && stops[i].StationId == stops[i - 1].StationId)
                errors.Add(new($"stops[{i}].stationId", "Hai điểm dừng liền kề không được cùng một bến."));
        }
        if (!stops[0].CanBoard) errors.Add(new("stops[0].canBoard", "Điểm dừng đầu phải cho khách lên tàu."));
        if (!stops[^1].CanAlight) errors.Add(new($"stops[{stops.Count - 1}].canAlight", "Điểm dừng cuối phải cho khách xuống tàu."));
        if (errors.Count > 0) throw new ValidationException(errors);
    }
}
