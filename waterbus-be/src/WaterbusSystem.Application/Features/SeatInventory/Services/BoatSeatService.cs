using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.SeatInventory;
using WaterbusSystem.Application.DTOs.Responses.SeatInventory;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.SeatInventory.Services;

/// <summary>
/// Cấu hình ghế thật của tàu. Bất biến: số ghế hoạt động &lt;= Boat.TotalSeats; không xoá cứng ghế (chỉ ngừng hoạt động),
/// ghế đang bị đặt/giữ trên chuyến chưa kết thúc không đổi mã/hạng và không ngừng được. Vé/đặt chỗ lịch sử giữ snapshot
/// (SeatCodeAtSale/SeatClassAtSale) nên không bị ảnh hưởng. Mọi thao tác chạy trong khoá thương mại (cùng khoá với đặt chỗ)
/// và sửa dòng Boat để các thao tác song song trên một tàu đụng RowVersion thay vì ghi chồng.
/// </summary>
public sealed class BoatSeatService(
    IBoatSeatRepository seats, IUnitOfWork unitOfWork, ICommercialTransactionRunner transactions) : IBoatSeatService
{
    public async Task<BoatSeatInventoryResponse> GetInventoryAsync(Guid boatId, CancellationToken cancellationToken = default)
    {
        var boat = await seats.GetBoatForUpdateAsync(boatId, cancellationToken);
        return await BuildAsync(boat, cancellationToken);
    }

    public Task<BoatSeatInventoryResponse> AddSeatsAsync(Guid boatId, AddBoatSeatsRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var boat = await seats.GetBoatForUpdateAsync(boatId, cancellationToken);
            var existing = await seats.ListAllTrackedAsync(boatId, cancellationToken);
            var classes = await RequireActiveClassesAsync(request.Seats.Select(s => s.SeatClassId), cancellationToken);

            var errors = new List<FluentValidation.Results.ValidationFailure>();
            var codes = existing.Select(s => s.SeatCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var positions = existing.Where(IsLive).Select(s => (s.RowNumber, s.ColumnNumber)).ToHashSet();
            var created = new List<Seat>();
            for (var i = 0; i < request.Seats.Count; i++)
            {
                var input = request.Seats[i];
                var code = input.SeatCode.Trim();
                if (!codes.Add(code)) errors.Add(new($"seats[{i}].seatCode", $"Mã ghế '{code}' đã tồn tại trên tàu hoặc bị trùng trong yêu cầu."));
                if (!positions.Add((input.RowNumber, input.ColumnNumber)))
                    errors.Add(new($"seats[{i}].position", $"Vị trí hàng {input.RowNumber}, cột {input.ColumnNumber} đã có ghế hoạt động."));
                if (!classes.ContainsKey(input.SeatClassId)) errors.Add(new($"seats[{i}].seatClassId", "Hạng ghế không tồn tại hoặc đã ngừng."));
                created.Add(new Seat
                {
                    BoatId = boatId, SeatCode = code, SeatClassId = input.SeatClassId,
                    RowNumber = input.RowNumber, ColumnNumber = input.ColumnNumber, IsActive = true
                });
            }
            if (errors.Count > 0) throw new ValidationException(errors);

            EnsureCapacity(boat, existing.Count(IsLive) + created.Count);
            seats.AddRange(created);
            Touch(boat);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildAsync(boat, cancellationToken);
        }, cancellationToken);

    public Task<BoatSeatInventoryResponse> UpdateSeatAsync(
        Guid boatId, Guid seatId, UpdateBoatSeatRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var boat = await seats.GetBoatForUpdateAsync(boatId, cancellationToken);
            var all = await seats.ListAllTrackedAsync(boatId, cancellationToken);
            var seat = FindSeat(all, seatId);
            var code = request.SeatCode.Trim();
            var identityChanged = !string.Equals(seat.SeatCode, code, StringComparison.Ordinal) || seat.SeatClassId != request.SeatClassId;

            var errors = new List<FluentValidation.Results.ValidationFailure>();
            if (all.Any(s => s.Id != seat.Id && string.Equals(s.SeatCode, code, StringComparison.OrdinalIgnoreCase)))
                errors.Add(new("seatCode", $"Mã ghế '{code}' đã tồn tại trên tàu."));
            if (seat.IsActive && all.Any(s => s.Id != seat.Id && IsLive(s) && s.RowNumber == request.RowNumber && s.ColumnNumber == request.ColumnNumber))
                errors.Add(new("position", $"Vị trí hàng {request.RowNumber}, cột {request.ColumnNumber} đã có ghế hoạt động."));
            if (seat.SeatClassId != request.SeatClassId
                && !(await RequireActiveClassesAsync([request.SeatClassId], cancellationToken)).ContainsKey(request.SeatClassId))
                errors.Add(new("seatClassId", "Hạng ghế không tồn tại hoặc đã ngừng."));
            if (errors.Count > 0) throw new ValidationException(errors);

            if (identityChanged) await EnsureNoOpenCommitmentsAsync(seat, "đổi mã/hạng", cancellationToken);

            seat.SeatCode = code;
            seat.SeatClassId = request.SeatClassId;
            seat.RowNumber = request.RowNumber;
            seat.ColumnNumber = request.ColumnNumber;
            seat.UpdatedAt = DateTimeOffset.UtcNow;
            Touch(boat);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildAsync(boat, cancellationToken);
        }, cancellationToken);

    public Task<BoatSeatInventoryResponse> DeactivateSeatAsync(Guid boatId, Guid seatId, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var boat = await seats.GetBoatForUpdateAsync(boatId, cancellationToken);
            var seat = FindSeat(await seats.ListAllTrackedAsync(boatId, cancellationToken), seatId);
            if (seat.IsActive)
            {
                await EnsureNoOpenCommitmentsAsync(seat, "ngừng", cancellationToken);
                seat.IsActive = false;
                seat.UpdatedAt = DateTimeOffset.UtcNow;
                Touch(boat);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return await BuildAsync(boat, cancellationToken);
        }, cancellationToken);

    public Task<BoatSeatInventoryResponse> ActivateSeatAsync(Guid boatId, Guid seatId, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var boat = await seats.GetBoatForUpdateAsync(boatId, cancellationToken);
            var all = await seats.ListAllTrackedAsync(boatId, cancellationToken);
            var seat = FindSeat(all, seatId);
            if (!seat.IsActive)
            {
                if (!(await RequireActiveClassesAsync([seat.SeatClassId], cancellationToken)).ContainsKey(seat.SeatClassId))
                    throw new ValidationException([new("seatClassId", "Hạng ghế đã ngừng; hãy đổi hạng hoặc kích hoạt lại hạng trước.")]);
                if (all.Any(s => s.Id != seat.Id && IsLive(s) && s.RowNumber == seat.RowNumber && s.ColumnNumber == seat.ColumnNumber))
                    throw new ValidationException([new("position", "Vị trí của ghế đã có ghế hoạt động khác.")]);
                EnsureCapacity(boat, all.Count(IsLive) + 1);
                seat.IsActive = true;
                seat.UpdatedAt = DateTimeOffset.UtcNow;
                Touch(boat);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return await BuildAsync(boat, cancellationToken);
        }, cancellationToken);

    // ---- helpers ----

    private static bool IsLive(Seat s) => s.IsActive && !s.IsDeleted;

    private static Seat FindSeat(List<Seat> all, Guid seatId) =>
        all.FirstOrDefault(s => s.Id == seatId && !s.IsDeleted) ?? throw new NotFoundException(nameof(Seat), seatId);

    private static void Touch(Boat boat) => boat.UpdatedAt = DateTimeOffset.UtcNow;

    private static void EnsureCapacity(Boat boat, int activeAfter)
    {
        if (activeAfter > boat.TotalSeats)
            throw new ConflictException(
                $"Số ghế hoạt động ({activeAfter}) vượt TotalSeats của tàu ({boat.TotalSeats}). Hãy tăng TotalSeats trước.");
    }

    private async Task<Dictionary<Guid, SeatClass>> RequireActiveClassesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = await seats.GetClassesAsync(ids.Distinct().ToList(), cancellationToken);
        return list.Where(c => c.IsActive && !c.IsDeleted).ToDictionary(c => c.Id);
    }

    private async Task EnsureNoOpenCommitmentsAsync(Seat seat, string action, CancellationToken cancellationToken)
    {
        if ((await seats.GetSeatIdsWithOpenCommitmentsAsync([seat.Id], cancellationToken)).Contains(seat.Id))
            throw new ConflictException($"Ghế '{seat.SeatCode}' đang được đặt/giữ trên chuyến chưa kết thúc nên không thể {action}.");
    }

    private async Task<BoatSeatInventoryResponse> BuildAsync(Boat boat, CancellationToken cancellationToken)
    {
        var list = await seats.ListAsync(boat.Id, cancellationToken);
        var active = list.Count(s => s.IsActive);
        return new(boat.Id, boat.TotalSeats, active, SeatInventoryRules.State(boat.TotalSeats, active), list);
    }
}

public static class SeatInventoryRules
{
    public const string NotConfigured = "NotConfigured";
    public const string Partial = "Partial";
    public const string Complete = "Complete";

    public static string State(int totalSeats, int activeSeats) =>
        activeSeats == 0 ? NotConfigured : activeSeats >= totalSeats ? Complete : Partial;
}
