using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Persistence;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.Infrastructure.Persistence;

public sealed class EfUnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Xung đột dữ liệu khi cập nhật ghế. Vui lòng thử lại!", ex);
        }
        catch (DbUpdateException ex)
        {
            // CHK-02/CHK-03: 2 request đồng thời cùng ghi 1 bản ghi vi phạm unique index (vd
            // ClientEventId của CheckInEvent, hoặc Primary Offline Scanner trùng điểm dừng) - trước
            // đây lỗi hạ tầng này rơi thẳng ra 500 cho TOÀN BỘ request, kể cả khi đang xử lý từng
            // event độc lập trong 1 batch (BR-OFF-03: 1 event lỗi không được làm fail các event
            // khác). Map về ConflictException (409, đã map sẵn ở GlobalExceptionHandlingMiddleware)
            // để caller tự quyết định: đọc lại bản ghi của bên thắng cho idempotent replay, hoặc báo
            // lỗi riêng cho đúng item đó.
            throw new ConflictException(
                "Dữ liệu vừa được ghi bởi một yêu cầu khác (trùng khoá duy nhất). Vui lòng thử lại.", ex);
        }
    }

    public void DiscardTrackedChanges() => context.ChangeTracker.Clear();
}
