namespace WaterbusSystem.Application.Features.SightseeingAudio.Models;

/// <summary>
/// Kịch bản đã được owner nội dung duyệt và khóa asset có version do caller cung cấp.
/// Service này không quyết định trạng thái publish của AudioGuide.
/// </summary>
public sealed record GenerateAudioAssetRequest(
    Guid PoiId,
    string ApprovedScriptText,
    string AssetKey);
