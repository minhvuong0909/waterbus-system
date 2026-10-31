namespace WaterbusSystem.Application.Common.Models;

/// <summary>Tàu đang hoạt động và thuyền trưởng được gán (để kiểm quyền gửi GPS).</summary>
public record BoatTrackingRef(Guid Id, Guid? CaptainUserId);

/// <summary>Thông tin ngắn của tàu đang hoạt động.</summary>
public record BoatSummary(Guid Id, string Code, string Name);
