namespace WaterbusSystem.Application.Interfaces.Persistence;

/// <summary>
/// Review PR #58 (High - "Enforce the complete Primary Offline Scanner overlap invariant"): loại
/// trừ lẫn nhau giữa các request đồng thời cùng gán Primary Offline Scanner cho ĐÚNG 1
/// TripStopCall. Check-then-insert (HasOverlappingPrimaryAssignmentAsync rồi mới Add) không tự
/// chặn được 2 request cùng pass bước kiểm tra trước khi bên nào insert xong; unique filtered
/// index ở DB (CHK-03) chỉ phủ đúng trường hợp Primary vô thời hạn (ActiveUntil IS NULL), không
/// phủ 2 khoảng hữu hạn (có ActiveUntil) chồng lấn giờ. Khoá theo TripStopCallId (khác điểm dừng
/// không chặn chéo nhau) giữ suốt từ lúc kiểm tra overlap tới khi SaveChangesAsync commit/rollback.
/// </summary>
public interface IScannerAssignmentLock
{
    /// <returns>Lease để giải phóng khoá, hoặc null nếu hết thời gian chờ mà request khác vẫn đang giữ khoá.</returns>
    Task<IAsyncDisposable?> TryAcquireAsync(Guid tripStopCallId, TimeSpan wait, CancellationToken cancellationToken = default);

    /// <summary>
    /// Review PR #63: khoá theo DeviceId, loại trừ lẫn nhau giữa gán phân công (Primary lẫn Secondary) và ngừng hoạt động
    /// CÙNG 1 thiết bị. Thiếu khoá này, AssignScanner thấy device còn Active rồi mới insert, trong lúc Deactivate đếm
    /// "0 phân công" và commit IsActive=false xen giữa -> phân công trên thiết bị đã ngừng. Thiết bị khác không chặn nhau.
    /// THỨ TỰ LẤY KHOÁ BẮT BUỘC: DeviceId -> TripStopCallId (-> khoá thương mại, nếu có). Không bao giờ lấy ngược.
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireDeviceAsync(Guid deviceId, TimeSpan wait, CancellationToken cancellationToken = default);
}
