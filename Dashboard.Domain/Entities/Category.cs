namespace Dashboard.Domain.Entities;

/// <summary>دسته‌بندی سلسله‌مراتبی برای فروشگاه (مثل «پوشاک > مردانه > تی‌شرت»)</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    public bool IsActive { get; set; } = true;
}