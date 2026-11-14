using WaterbusSystem.Domain.Common;
using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Entities;

/// <summary>
/// Sổ cái lệnh phát audio Sightseeing tới Onboard Player (BR-EXP-03/05). Một dòng = một lần ghé POI trên một Trip:
/// UNIQUE (TripId, RoutePoiKey, VisitKey) là chốt chặn chống phát trùng bền vững qua restart/nhiều instance.
/// <see cref="BaseEntity.Id"/> chính là RequestId ổn định gửi cho player, nên retry luôn cùng một định danh.
/// Backend chỉ ghi nhận player ĐÃ NHẬN/BÁO trạng thái; không bao giờ tự khẳng định đã phát ra loa.
/// </summary>
public class AudioPlaybackRequest : BaseEntity
{
    public Guid TripId { get; set; }
    /// <summary>RoutePoi.Id (hoặc PoiId khi dữ liệu cũ thiếu RoutePoi) — cùng POI xuất hiện nhiều lần trên tuyến vẫn tách biệt.</summary>
    public Guid RoutePoiKey { get; set; }
    /// <summary>Định danh lượt ghé cho hành trình vòng; chuỗi rỗng = lượt mặc định.</summary>
    public string VisitKey { get; set; } = string.Empty;

    public Guid? BoatId { get; set; }
    public Guid? PoiId { get; set; }
    public Guid? AudioGuideId { get; set; }
    /// <summary>Định danh bền vững của asset (không phải URL có chữ ký).</summary>
    public string? AudioAssetUri { get; set; }
    public int? DurationSeconds { get; set; }
    public double? DistanceMeters { get; set; }

    public AudioPlaybackStatus Status { get; set; } = AudioPlaybackStatus.Reserved;
    /// <summary>Số lần lệnh THỰC SỰ được gửi tới player (không tính lần player offline/asset lỗi trước khi gửi).</summary>
    public int Attempts { get; set; }
    /// <summary>
    /// Epoch sở hữu reservation: tăng 1 mỗi lần reserve/giành lại thành công. Dispatch/release/accept của evaluation
    /// phải mang đúng epoch (fencing token) nên evaluation đã mất lease không thể gửi hay nhả reservation của người mới.
    /// </summary>
    public int ReservationEpoch { get; set; }
    public DateTimeOffset ReservedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    /// <summary>Chẩn đoán đã làm sạch (không chứa URL/token).</summary>
    public string? FailureReason { get; set; }
}
