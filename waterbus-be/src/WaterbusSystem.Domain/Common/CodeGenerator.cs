using System.Security.Cryptography;

namespace WaterbusSystem.Domain.Common;

/// <summary>
/// Sinh mã nghiệp vụ (BookingCode/TicketCode) chống va chạm:
/// Timestamp (giây) đảm bảo sắp xếp theo thời gian + hậu tố ngẫu nhiên CSPRNG đủ dài
/// để xác suất trùng mã giữa 2 lần sinh gần như bằng 0 (32^6 ≈ 1 tỷ tổ hợp hậu tố).
/// Không dùng Random.Shared vì không phải CSPRNG và không đủ entropy để tránh va chạm khi tải cao.
/// </summary>
public static class CodeGenerator
{
    // Bỏ các ký tự dễ gây nhầm lẫn khi đọc bằng mắt: 0/O, 1/I/L
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int SuffixLength = 6;

    public static string GenerateBookingCode(DateTimeOffset now) =>
        $"WB{now:yyyyMMddHHmmss}{RandomNumberGenerator.GetString(Alphabet, SuffixLength)}";

    public static string GenerateTicketCode(DateTimeOffset now) =>
        $"TK{now:yyyyMMddHHmmss}{RandomNumberGenerator.GetString(Alphabet, SuffixLength)}";
}
