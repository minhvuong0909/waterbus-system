namespace WaterbusSystem.Application.Interfaces.Persistence;

/// <summary>
/// Review PR #81: "chưa có bến nào trùng tọa độ" là bất biến mà DB không tự bảo vệ được - tọa độ lưu <c>float</c> và quy tắc
/// "cùng điểm" là so sánh theo ngưỡng (không phải bằng nhau tuyệt đối) nên unique index không biểu diễn được. Check-then-insert
/// (ExistsAtCoordinatesAsync rồi mới Add) để 2 request đồng thời cùng pass bước kiểm tra. Khoá này loại trừ lẫn nhau giữa các
/// request tạo/sửa/bật lại bến, giữ từ lúc kiểm tra tới khi SaveChangesAsync commit/rollback. Một khoá chung cho mọi bến vì
/// "lân cận" theo ngưỡng không chia được thành khoá theo key; thao tác quản trị bến hiếm nên không đáng kể.
/// </summary>
public interface IStationCoordinateLock
{
    /// <returns>Lease để giải phóng khoá, hoặc null nếu hết thời gian chờ mà request khác vẫn đang giữ khoá.</returns>
    Task<IAsyncDisposable?> TryAcquireAsync(TimeSpan wait, CancellationToken cancellationToken = default);
}
