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

/// <summary>Quản lý hạng ghế (BR-SEAT-01). Ngừng hạng ghế chạy trong khoá thương mại để không đua với checkout.</summary>
public sealed class SeatClassService(
    ISeatClassRepository classes, IUnitOfWork unitOfWork, ICommercialTransactionRunner transactions) : ISeatClassService
{
    public Task<List<SeatClassResponse>> ListAsync(GetSeatClassesRequest request, CancellationToken cancellationToken = default) =>
        classes.ListAsync(request.IncludeInactive, cancellationToken);

    public Task<SeatClassResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        classes.GetDetailAsync(id, cancellationToken);

    public async Task<Guid> CreateAsync(CreateSeatClassRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        if (await classes.ExistsByCodeAsync(code, cancellationToken))
            throw new ValidationException([new("code", $"Mã hạng ghế '{code}' đã tồn tại.")]);

        var seatClass = new SeatClass { Code = code, Name = request.Name.Trim(), Description = request.Description?.Trim(), IsActive = true };
        classes.Add(seatClass);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return seatClass.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateSeatClassRequest request, CancellationToken cancellationToken = default)
    {
        var seatClass = await classes.GetRequiredByIdAsync(id, cancellationToken);
        seatClass.Name = request.Name.Trim();
        seatClass.Description = request.Description?.Trim();
        seatClass.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        transactions.RunAsync(async () =>
        {
            var seatClass = await classes.GetRequiredByIdAsync(id, cancellationToken);
            if (!seatClass.IsActive) return true;
            if (await classes.HasOpenCommitmentsAsync(id, cancellationToken))
                throw new ConflictException("Hạng ghế đang có ghế được đặt/giữ trên chuyến chưa kết thúc nên chưa thể ngừng.");
            seatClass.IsActive = false;
            seatClass.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var seatClass = await classes.GetRequiredByIdAsync(id, cancellationToken);
        if (seatClass.IsActive) return;
        seatClass.IsActive = true;
        seatClass.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
