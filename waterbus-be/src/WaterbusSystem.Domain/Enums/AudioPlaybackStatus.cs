namespace WaterbusSystem.Domain.Enums;

/// <summary>Trạng thái lệnh phát audio. Không đổi số khi đã phát hành.</summary>
public enum AudioPlaybackStatus
{
    /// <summary>Đã giữ chỗ lượt ghé, đang gửi lệnh tới player (có lease hết hạn).</summary>
    Reserved = 1,
    /// <summary>Player đã nhận và chấp nhận xếp lệnh (chưa chắc đã phát).</summary>
    Accepted = 2,
    /// <summary>Player báo đã bắt đầu phát.</summary>
    Started = 3,
    /// <summary>Player báo phát xong.</summary>
    Completed = 4,
    /// <summary>Gửi lỗi/không ACK — được phép giữ chỗ lại (còn lượt thử).</summary>
    Released = 5,
    /// <summary>Hết lượt thử hoặc player báo lỗi cuối: không tự phát lại trong lượt ghé này.</summary>
    Failed = 6
}
