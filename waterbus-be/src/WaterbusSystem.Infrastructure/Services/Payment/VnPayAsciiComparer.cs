namespace WaterbusSystem.Infrastructure.Services.Payment;

/// <summary>
/// Sắp xếp tên tham số theo thứ tự bảng mã ASCII Alphabetical theo đúng đặc tả của cổng VNPAY.
/// LƯU Ý: Class này chỉ phục vụ sắp xếp tham số khi nối chuỗi băm (SortedList), KHÔNG dùng để so sánh hash!
/// </summary>
public class VnPayAsciiComparer : IComparer<string>
{
    public int Compare(string? x, string? y) => string.CompareOrdinal(x, y);
}
