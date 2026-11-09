using System.Security.Cryptography;
using System.Text;

namespace WaterbusSystem.Application.Features.SightseeingAudio;

/// <summary>RequestId tất định cho một lượt ghé POI: mọi lần retry cùng lượt dùng cùng Id để player khử trùng.</summary>
public static class AudioRequestIds
{
    public static Guid For(Guid tripId, Guid routePoiKey, string? visitId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{tripId:N}:{routePoiKey:N}:{(visitId is null ? "0" : "1:" + visitId)}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
