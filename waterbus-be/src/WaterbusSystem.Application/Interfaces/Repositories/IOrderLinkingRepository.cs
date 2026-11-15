using WaterbusSystem.Domain.Entities;

namespace WaterbusSystem.Application.Interfaces.Repositories;

/// <summary>Truy vấn đơn chưa gắn tài khoản theo email người mua (dùng index lọc trên PurchaserEmail).</summary>
public interface IOrderLinkingRepository
{
    /// <summary>Đơn chưa gắn tài khoản, ở trạng thái không còn trong quá trình thanh toán (Confirmed, Cancelled, Expired).</summary>
    Task<int> CountLinkableAsync(string email, CancellationToken ct);

    /// <summary>Đơn như trên, được theo dõi để gán trong cùng transaction. Cũ nhất trước, tối đa <paramref name="take"/>.</summary>
    Task<List<PurchaseOrder>> GetLinkableTrackedAsync(string email, int take, CancellationToken ct);
}
