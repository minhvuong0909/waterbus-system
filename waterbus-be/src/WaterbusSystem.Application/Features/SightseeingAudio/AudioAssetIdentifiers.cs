namespace WaterbusSystem.Application.Features.SightseeingAudio;

/// <summary>
/// Kiểm tra định danh asset audio có ổn định để dùng lâu dài hay không
/// (không phải URL có chữ ký, không phải đường dẫn cục bộ).
/// </summary>
public static class AudioAssetIdentifiers
{
    public static bool IsStable(string? assetUri) =>
        !string.IsNullOrWhiteSpace(assetUri)
        && !assetUri.Contains('?') && !assetUri.Contains('#')
        && !assetUri.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
        && !assetUri.StartsWith('/') && !assetUri.StartsWith('~')
        && !(assetUri.Length >= 2 && char.IsLetter(assetUri[0]) && assetUri[1] == ':')
        && !assetUri.Contains('\\');

    /// <summary>Khoá tương đối của asset trong kho lưu trữ, phục vụ đọc MP3 theo tên.</summary>
    public static string ToAssetKey(string? assetUri)
    {
        if (string.IsNullOrWhiteSpace(assetUri)) return string.Empty;
        const string marker = "/audio-assets/";
        var index = assetUri.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? assetUri[(index + marker.Length)..] : assetUri;
    }
}
