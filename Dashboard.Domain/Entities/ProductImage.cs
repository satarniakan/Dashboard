namespace Dashboard.Domain.Entities;

public class ProductImage
{
    public int Id { get; set; }

    public int ProductGroupId { get; set; }
    public ProductGroup? ProductGroup { get; set; }

    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}