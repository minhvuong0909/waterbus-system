using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace WaterbusSystem.Application.Features.Notifications.Templates;

/// <summary>Thông tin thương hiệu/liên hệ dùng chung cho các template (chỉ giá trị thật từ cấu hình).</summary>
public sealed record EmailContext(string BrandName, string? PublicBaseUrl, string? SupportEmail, string? SupportPhone)
{
    public static EmailContext From(EmailOptions o) => new(
        string.IsNullOrWhiteSpace(o.FromName) ? "Smart Waterbus" : o.FromName.Trim(),
        string.IsNullOrWhiteSpace(o.PublicBaseUrl) ? null : o.PublicBaseUrl.Trim().TrimEnd('/'),
        string.IsNullOrWhiteSpace(o.SupportEmail) ? null : o.SupportEmail.Trim(),
        string.IsNullOrWhiteSpace(o.SupportPhone) ? null : o.SupportPhone.Trim());
}

public static class EmailFormat
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7); // Asia/Ho_Chi_Minh, không có DST

    // Chỉ escape ký tự nguy hiểm (< > & " '), giữ nguyên chữ tiếng Việt thay vì đổi sang &#NNN;.
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static string Html(string? value) => Encoder.Encode(value ?? string.Empty);

    /// <summary>VND: "15.000 ₫" (dấu chấm ngăn nghìn, tất định, không phụ thuộc ICU của máy chủ).</summary>
    public static string Money(decimal amount, string currency) =>
        string.Equals(currency, "VND", StringComparison.OrdinalIgnoreCase)
            ? amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.') + " ₫"
            : amount.ToString("N2", CultureInfo.InvariantCulture) + " " + currency;

    public static string Time(DateTimeOffset value) =>
        value.ToOffset(Vietnam).ToString("HH:mm 'ngày' dd/MM/yyyy", CultureInfo.InvariantCulture) + " (GMT+7)";

    /// <summary>Chuỗi một dòng an toàn cho header/subject (không CR/LF).</summary>
    public static string SingleLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}

/// <summary>
/// Dựng đồng thời HTML (table + inline CSS, tương thích client email, không script) và bản text thuần.
/// Mọi giá trị chèn vào đều được HTML-encode tại đây, nên template không thể quên escape.
/// </summary>
public sealed class EmailBodyBuilder(EmailContext context)
{
    private readonly StringBuilder _html = new();
    private readonly StringBuilder _text = new();
    private const string Font = "font-family:Arial,Helvetica,sans-serif;";

    public EmailBodyBuilder Heading(string text)
    {
        _html.Append($"<h2 style=\"{Font}font-size:18px;color:#0b3c5d;margin:24px 0 8px;\">{EmailFormat.Html(text)}</h2>");
        _text.AppendLine().AppendLine(text.ToUpperInvariant()).AppendLine(new string('-', Math.Min(text.Length, 60)));
        return this;
    }

    public EmailBodyBuilder Paragraph(string text)
    {
        _html.Append($"<p style=\"{Font}font-size:15px;line-height:1.5;color:#1f2933;margin:8px 0;\">{EmailFormat.Html(text)}</p>");
        _text.AppendLine(text).AppendLine();
        return this;
    }

    public EmailBodyBuilder Notice(string text)
    {
        _html.Append($"<div style=\"{Font}font-size:14px;line-height:1.5;color:#5c3d00;background:#fff4d6;border:1px solid #f0c36d;padding:10px 12px;margin:12px 0;\">{EmailFormat.Html(text)}</div>");
        _text.AppendLine("! " + text).AppendLine();
        return this;
    }

    public EmailBodyBuilder Fact(string label, string value)
    {
        _html.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"{Font}font-size:14px;\"><tr>" +
            $"<td width=\"40%\" valign=\"top\" style=\"color:#52606d;padding:3px 8px 3px 0;\">{EmailFormat.Html(label)}</td>" +
            $"<td valign=\"top\" style=\"color:#1f2933;padding:3px 0;font-weight:bold;\">{EmailFormat.Html(value)}</td></tr></table>");
        _text.AppendLine($"{label}: {value}");
        return this;
    }

    public EmailBodyBuilder Divider()
    {
        _html.Append("<hr style=\"border:0;border-top:1px solid #d9e2ec;margin:16px 0;\">");
        _text.AppendLine();
        return this;
    }

    /// <summary>Ảnh nhúng theo CID; bản text chỉ ghi chú vì không có ảnh.</summary>
    public EmailBodyBuilder InlineImage(string contentId, string altText, int size, string textNote)
    {
        _html.Append($"<p style=\"margin:8px 0;\"><img src=\"cid:{EmailFormat.Html(contentId)}\" alt=\"{EmailFormat.Html(altText)}\" width=\"{size}\" height=\"{size}\" style=\"display:block;border:1px solid #d9e2ec;\"></p>");
        _text.AppendLine(textNote);
        return this;
    }

    public EmailBodyBuilder Link(string label, string url)
    {
        _html.Append($"<p style=\"{Font}font-size:15px;margin:8px 0;\"><a href=\"{EmailFormat.Html(url)}\" style=\"color:#0a66c2;\">{EmailFormat.Html(label)}</a></p>");
        _text.AppendLine($"{label}: {url}");
        return this;
    }

    /// <summary>Đoạn "liên hệ hỗ trợ": chỉ liệt kê kênh thật đã cấu hình, không bịa.</summary>
    public string SupportSentence()
    {
        var channels = new List<string>();
        if (context.SupportEmail is not null) channels.Add("email " + context.SupportEmail);
        if (context.SupportPhone is not null) channels.Add("số " + context.SupportPhone);
        return channels.Count == 0
            ? $"Vui lòng liên hệ bộ phận hỗ trợ {context.BrandName}"
            : $"Vui lòng liên hệ bộ phận hỗ trợ {context.BrandName} qua " + string.Join(" hoặc ", channels);
    }

    public (string Html, string Text) Render(string title)
    {
        var html = $"<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            $"<title>{EmailFormat.Html(title)}</title></head><body style=\"margin:0;padding:0;background:#f0f4f8;\">" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr><td align=\"center\" style=\"padding:16px 8px;\">" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;background:#ffffff;\">" +
            $"<tr><td style=\"background:#0b3c5d;padding:16px 20px;{Font}font-size:20px;font-weight:bold;color:#ffffff;\">{EmailFormat.Html(context.BrandName)}</td></tr>" +
            $"<tr><td style=\"padding:8px 20px 20px;\"><h1 style=\"{Font}font-size:22px;color:#0b3c5d;margin:16px 0 8px;\">{EmailFormat.Html(title)}</h1>{_html}</td></tr>" +
            $"<tr><td style=\"padding:12px 20px;background:#f0f4f8;{Font}font-size:12px;color:#52606d;\">Đây là email tự động từ {EmailFormat.Html(context.BrandName)}. Vui lòng không chia sẻ nội dung email này, đặc biệt là mã QR.</td></tr>" +
            "</table></td></tr></table></body></html>";
        var text = $"{context.BrandName}\n{title}\n{new string('=', Math.Min(title.Length, 60))}\n{_text}\n--\nĐây là email tự động từ {context.BrandName}. Vui lòng không chia sẻ nội dung email này, đặc biệt là mã QR.\n";
        return (html, text);
    }
}
