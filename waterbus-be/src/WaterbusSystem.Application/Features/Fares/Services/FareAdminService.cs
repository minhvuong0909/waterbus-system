using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.Fares;
using WaterbusSystem.Application.DTOs.Responses.Fares;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Application.Features.Fares.Services;

/// <summary>
/// Quản lý FareRule (BR-FARE-01..04, BR-CFG-01/02). Giá chỉ theo TripType x SeatClass, VND. Không cho hai bảng giá đang bật cùng
/// phạm vi giao khoảng hiệu lực (mơ hồ: Seat Map và Checkout đều từ chối). Bảng giá đã có hiệu lực chỉ được kết thúc sớm, không đổi giá;
/// lịch sử (QuotedFare/PaidAllocation/Ticket snapshot) nằm ở giao dịch nên không bao giờ bị sửa theo.
/// </summary>
public sealed class FareAdminService(
    IFareAdminRepository fares, IUnitOfWork unitOfWork, ICommercialTransactionRunner transactions,
    IDateTimeService dateTime, ICurrentUserService currentUser) : IFareAdminService
{
    public Task<PaginatedList<FareRuleAdminResponse>> GetPageAsync(GetFareRulesRequest request, CancellationToken cancellationToken = default) =>
        fares.GetPageAsync(request, dateTime.Now, cancellationToken);

    public Task<FareRuleAdminResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        fares.GetDetailAsync(id, dateTime.Now, cancellationToken);

    public Task<Guid> CreateAsync(CreateFareRuleRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var seatClass = await fares.FindSeatClassAsync(request.SeatClassId, cancellationToken);
            if (seatClass is not { IsActive: true }) throw new ValidationException([new("seatClassId", "Hạng ghế không tồn tại hoặc đã ngừng.")]);
            await EnsureNoConflictAsync(request.TripType, request.SeatClassId, request.EffectiveFrom, request.EffectiveTo, null, true, cancellationToken);

            var fare = new FareRule
            {
                TripType = request.TripType, SeatClassId = request.SeatClassId, Price = request.Price, Currency = request.Currency,
                EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, IsActive = true,
                CreatedByAdminId = Guid.TryParse(currentUser.UserId, out var admin) ? admin : null
            };
            fares.Add(fare);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return fare.Id;
        }, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateFareRuleRequest request, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var fare = await fares.GetTrackedAsync(id, cancellationToken);
            var started = fare.EffectiveFrom <= dateTime.Now;
            if (started && (request.Price != fare.Price || request.EffectiveFrom != fare.EffectiveFrom))
                throw new ConflictException("Bảng giá đã có hiệu lực: chỉ đổi được EffectiveTo. Muốn đổi giá hãy tạo bảng giá mới.");
            // Unique index (TripType, SeatClassId, EffectiveFrom) tính cả bảng đã tắt nên luôn kiểm khi đổi EffectiveFrom;
            // chỉ xét giao khoảng hiệu lực với bảng đang bật khi chính bảng này đang bật.
            if (request.EffectiveFrom != fare.EffectiveFrom
                && await fares.ExistsWithSameStartAsync(fare.TripType, fare.SeatClassId, request.EffectiveFrom, id, cancellationToken))
                throw new ConflictException("Đã có bảng giá cùng loại chuyến, hạng ghế và thời điểm bắt đầu (kể cả bảng đã tắt).");
            if (fare.IsActive)
                await EnsureNoConflictAsync(fare.TripType, fare.SeatClassId, request.EffectiveFrom, request.EffectiveTo, id, false, cancellationToken);

            fare.Price = request.Price;
            fare.EffectiveFrom = request.EffectiveFrom;
            fare.EffectiveTo = request.EffectiveTo;
            fare.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    /// <summary>Tắt bảng giá: hạng ghế đó có thể thành không bán được nếu không còn bảng giá khác (Search/Seat Map sẽ ẩn).</summary>
    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var fare = await fares.GetTrackedAsync(id, cancellationToken);
            if (!fare.IsActive) return true;
            fare.IsActive = false;
            fare.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    private async Task EnsureNoConflictAsync(
        string tripType, Guid seatClassId, DateTimeOffset from, DateTimeOffset? to, Guid? excludeId, bool checkSameStart, CancellationToken cancellationToken)
    {
        if (checkSameStart && await fares.ExistsWithSameStartAsync(tripType, seatClassId, from, excludeId, cancellationToken))
            throw new ConflictException("Đã có bảng giá cùng loại chuyến, hạng ghế và thời điểm bắt đầu (kể cả bảng đã tắt).");
        foreach (var other in await fares.ListActiveSiblingsAsync(tripType, seatClassId, excludeId, cancellationToken))
        {
            var overlaps = from < (other.EffectiveTo ?? DateTimeOffset.MaxValue) && other.EffectiveFrom < (to ?? DateTimeOffset.MaxValue);
            if (overlaps)
                throw new ConflictException(
                    $"Khoảng hiệu lực giao với bảng giá đang bật khác ({other.EffectiveFrom:u} → {(other.EffectiveTo?.ToString("u") ?? "không giới hạn")}). Hãy kết thúc bảng giá cũ trước.");
        }
    }
}
