namespace Dashboard.Domain.Entities;

/// <summary>سند حسابداری — سرِ سند. جمع بدهکار سطرها همیشه باید با جمع بستانکار برابر باشد.</summary>
public class JournalEntry
{
    public int Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;

    // اشاره به سند مبدأ (مثلاً فاکتور فروش)، برای ردیابی
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    public bool IsSystemGenerated { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<JournalEntryLine> Lines { get; set; } = new();
}

public class JournalEntryLine
{
    public int Id { get; set; }
    public int JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public int AccountId { get; set; }
    public Account? Account { get; set; }

    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
    public string? SubsidiaryType { get; set; }  // "Customer" یا "Supplier"
    public int? SubsidiaryId { get; set; }
}