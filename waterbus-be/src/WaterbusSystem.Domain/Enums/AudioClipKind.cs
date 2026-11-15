namespace WaterbusSystem.Domain.Enums;

/// <summary>Loại thông báo audio phát trên loa tàu. KHÔNG đổi số đã có.</summary>
public enum AudioClipKind
{
    StationApproaching = 1,
    StationArrived = 2,
    StationDeparted = 3,
    Poi = 4
}
