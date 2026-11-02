namespace WaterbusSystem.Application.Interfaces.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bỏ theo dõi mọi entity đang pending (Added/Modified) sau khi SaveChangesAsync thất bại, để các
    /// entity dở dang của lần xử lý trước không bị gộp nhầm vào lần SaveChangesAsync kế tiếp trên cùng
    /// Unit of Work (ví dụ xử lý từng item độc lập trong 1 vòng lặp - xem CheckInService.SyncCheckInEventsAsync).
    /// </summary>
    void DiscardTrackedChanges();
}