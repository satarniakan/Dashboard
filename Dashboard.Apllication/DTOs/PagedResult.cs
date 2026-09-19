namespace Dashboard.Application.DTOs;

// یک نتیجه‌ی صفحه‌بندی‌شده‌ی عمومی که هر لیستی (محصولات، مشتریان، فاکتورها، ...)
// می‌تواند از آن استفاده کند، بدون نیاز به ساختن یک کلاس جدا برای هر کدام.
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
