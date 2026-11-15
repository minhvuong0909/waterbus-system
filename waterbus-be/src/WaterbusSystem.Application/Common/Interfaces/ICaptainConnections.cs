namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>Registry kết nối Captain trong deployment SignalR một instance hiện tại.</summary>
public interface ICaptainConnections
{
    void Register(Guid captainId, string connectionId, Action abort);
    void Unregister(string connectionId);
    void Revoke(Guid captainId);
}
