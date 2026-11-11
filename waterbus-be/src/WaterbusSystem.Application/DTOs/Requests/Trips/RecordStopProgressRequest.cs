using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Application.DTOs.Requests.Trips;

/// <summary>Ghi nhận tàu đã đến/rời một điểm dừng cụ thể của chuyến (BR-STOP-02).</summary>
public record RecordStopProgressRequest(StopCallProgress Action);
