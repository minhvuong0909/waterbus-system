namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>Chờ ACK "đã nhận lệnh" của Onboard Player cho một RequestId.</summary>
public interface IOnboardAckWait : IDisposable
{
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>Hiện diện và ACK của Onboard Player theo Boat (in-process; SignalR hiện chạy một instance).</summary>
public interface IOnboardPlayerRegistry
{
    bool IsConnected(Guid boatId);
    /// <summary>Kết nối đang đăng ký cho Boat kèm Captain đã xác thực lúc kết nối.</summary>
    IReadOnlyList<(string ConnectionId, Guid CaptainId)> Connections(Guid boatId);
    void Unregister(string connectionId);
    /// <summary>Chính sách một player hoạt động mỗi Boat: kết nối mới nhất nhận lệnh phát; kết nối cũ chỉ ở chế độ chờ.</summary>
    bool IsActive(string connectionId);
    IOnboardAckWait PrepareAck(Guid requestId);
    /// <summary>Hoàn tất lượt chờ ACK; false nếu không có dispatch nào đang chờ (ACK muộn/lặp).</summary>
    bool SignalAck(Guid requestId);
}
