using FluentValidation;
using System.Text.RegularExpressions;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Application.Common.Validators;

/// <summary>
/// Validator kiểm tra tính hợp lệ của StoreAudioCommand trước khi đưa vào kho lưu trữ.
/// Đảm bảo dữ liệu MP3 đúng quy cách và AssetKey an toàn (ngăn path traversal).
/// </summary>
public class StoreAudioCommandValidator : AbstractValidator<StoreAudioCommand>
{
    private const int MaxAudioSizeBytes = 50 * 1024 * 1024; // 50 MB tối đa cho audio thuyết minh
    private static readonly Regex SafeAssetKey = new(
        "^[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*\\.mp3$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public StoreAudioCommandValidator()
    {
        RuleFor(x => x.AudioData)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Dữ liệu âm thanh không được để null.")
            .NotEmpty().WithMessage("Dữ liệu âm thanh không được để rỗng.")
            .Must(data => data.Length <= MaxAudioSizeBytes)
            .WithMessage("Kích thước tệp âm thanh vượt quá giới hạn tối đa 50MB.")
            .Must(HasMp3Signature)
            .WithMessage("Dữ liệu âm thanh không có header MP3 ID3/MPEG hợp lệ.");

        RuleFor(x => x.ContentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("ContentType không được để trống.")
            .Must(ct => string.Equals(ct, "audio/mpeg", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Định dạng âm thanh bắt buộc phải là 'audio/mpeg' (MP3).");

        RuleFor(x => x.AssetKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Khóa tệp (AssetKey) không được để trống.")
            .MaximumLength(400).WithMessage("Khóa tệp không được vượt quá 400 ký tự (đảm bảo URI đầy đủ không vượt quá 500 ký tự).")
            .Must(key => SafeAssetKey.IsMatch(key))
            .WithMessage("AssetKey chỉ được có các segment chữ/số, '-' hoặc '_', ngăn cách bằng '/', và kết thúc bằng '.mp3'.");

        RuleFor(x => x.LanguageCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mã ngôn ngữ không được để trống.")
            .Must(lang => lang == "vi")
            .WithMessage("Phạm vi hiện tại chỉ hỗ trợ mã ngôn ngữ tiếng Việt ('vi').");

        RuleFor(x => x.PoiId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("PoiId không được là Guid rỗng.")
            .When(x => x.PoiId.HasValue);

        RuleFor(x => x.Duration)
            .Must(duration => !duration.HasValue || duration.Value > TimeSpan.Zero)
            .WithMessage("Thời lượng âm thanh phải dương khi được cung cấp.");
    }

    private static bool HasMp3Signature(byte[] data) =>
        (data.Length >= 3 && data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3')
        || (data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0);
}
