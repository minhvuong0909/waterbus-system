namespace WaterbusSystem.Application.Common.Models;

/// <summary>
/// Model phân trang dùng chung cho các truy vấn danh sách (GetList queries). Chỉ là data shape thuần -
/// Repository ở Infrastructure chịu trách nhiệm thực thi truy vấn (EF Core) rồi dựng instance này.
/// </summary>
public class PaginatedList<T>
{
    public IReadOnlyCollection<T> Items { get; }
    public int PageNumber { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }

    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PaginatedList(IReadOnlyCollection<T> items, int count, int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        TotalCount = count;
        Items = items;
    }
}
