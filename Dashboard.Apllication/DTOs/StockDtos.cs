using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

/// <summary>
/// اعتبارسنجی ردیف‌های کالای اسناد انبار.
/// چون Blazor فقط attribute های خودِ DTO را بررسی می‌کند (نه اعضای داخل لیست)،
/// قواعد ردیف‌ها از طریق IValidatableObject اعمال می‌شود تا پیامشان در ValidationSummary نمایش داده شود.
/// </summary>
internal static class StockItemValidation
{
    public static IEnumerable<ValidationResult> Validate(
        IEnumerable<(int ProductId, decimal Quantity)>? rows)
    {
        var items = rows?.ToList() ?? new List<(int ProductId, decimal Quantity)>();

        if (items.Count == 0)
        {
            yield return new ValidationResult("حداقل یک قلم کالا لازم است.", new[] { "Items" });
            yield break;
        }

        var rowNumber = 0;
        foreach (var (productId, quantity) in items)
        {
            rowNumber++;
            var row = ToPersianDigits(rowNumber);

            if (productId <= 0)
            {
                yield return new ValidationResult(
                    $"کالای ردیف {row} را انتخاب کنید.", new[] { "Items" });
            }

            if (quantity <= 0)
            {
                yield return new ValidationResult(
                    $"مقدار ردیف {row} باید بزرگتر از صفر باشد.", new[] { "Items" });
            }
        }
    }

    // پیام‌ها هم‌زبانِ بقیه‌ی رابط کاربری باشند (ارقام فارسی).
    private static string ToPersianDigits(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            .Replace('0', '۰').Replace('1', '۱').Replace('2', '۲').Replace('3', '۳')
            .Replace('4', '۴').Replace('5', '۵').Replace('6', '۶').Replace('7', '۷')
            .Replace('8', '۸').Replace('9', '۹');
}

public record StockLevelDto(
    int ProductId,
    string ProductName,
    string Sku,
    int WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    int ReorderPoint);

public record StockTransactionDto(
    int Id,
    string ProductName,
    string WarehouseName,
    string Type,
    decimal QuantityChange,
    decimal? UnitCost,
    string? Notes,
    string? CreatedByUserId,
    DateTime OccurredAt);

public class StockItemInput
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب کالا الزامی است.")]
    public int ProductId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "مقدار باید بزرگتر از صفر باشد.")]
    public decimal Quantity { get; set; }
}

public class PurchaseReceiptItemInput
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب کالا الزامی است.")]
    public int ProductId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "مقدار باید بزرگتر از صفر باشد.")]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "بهای تمام‌شده نمی‌تواند منفی باشد.")]
    public decimal UnitCost { get; set; }
}

public class CreatePurchaseReceiptDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب تأمین‌کننده الزامی است.")]
    public int SupplierId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب انبار مقصد الزامی است.")]
    public int WarehouseId { get; set; }

    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }

    [MinLength(1, ErrorMessage = "حداقل یک قلم کالا لازم است.")]
    public List<PurchaseReceiptItemInput> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => StockItemValidation.Validate(Items?.Select(i => (i.ProductId, i.Quantity)));
}

public class CreateInternalIssueDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب انبار الزامی است.")]
    public int WarehouseId { get; set; }

    public DateTime IssueDate { get; set; } = DateTime.UtcNow;

    [StringLength(200, ErrorMessage = "هدف مصرف نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
    public string? Purpose { get; set; }

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }

    [MinLength(1, ErrorMessage = "حداقل یک قلم کالا لازم است.")]
    public List<StockItemInput> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => StockItemValidation.Validate(Items?.Select(i => (i.ProductId, i.Quantity)));
}

public class CreateSalesReturnDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "انبار فاکتور تعیین نشده است؛ ابتدا فاکتور را جستجو کنید.")]
    public int WarehouseId { get; set; }

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    [StringLength(100, ErrorMessage = "مرجع مشتری نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string? CustomerReference { get; set; }

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }

    public int? SalesInvoiceId { get; set; }

    [MinLength(1, ErrorMessage = "حداقل یک قلم کالا لازم است.")]
    public List<StockItemInput> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => StockItemValidation.Validate(Items?.Select(i => (i.ProductId, i.Quantity)));
}

public class CreateScrapRecordDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب انبار الزامی است.")]
    public int WarehouseId { get; set; }

    public DateTime RecordDate { get; set; } = DateTime.UtcNow;

    [StringLength(200, ErrorMessage = "دلیل نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
    public string? Reason { get; set; }

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }

    [MinLength(1, ErrorMessage = "حداقل یک قلم کالا لازم است.")]
    public List<StockItemInput> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => StockItemValidation.Validate(Items?.Select(i => (i.ProductId, i.Quantity)));
}

public class CreateStockTransferDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب انبار مبدأ الزامی است.")]
    public int SourceWarehouseId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب انبار مقصد الزامی است.")]
    public int DestinationWarehouseId { get; set; }

    public DateTime TransferDate { get; set; } = DateTime.UtcNow;

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }

    [MinLength(1, ErrorMessage = "حداقل یک قلم کالا لازم است.")]
    public List<StockItemInput> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => StockItemValidation.Validate(Items?.Select(i => (i.ProductId, i.Quantity)));
}

public record StockCountItemDto(int ProductId, string ProductName, decimal SystemQuantity, decimal? CountedQuantity);

public record StockCountDto(int Id, string CountNumber, string WarehouseName, string Status, DateTime CountDate, List<StockCountItemDto> Items);

// --- خلاصه‌ی اسناد برای صفحات لیست ---

public record PurchaseReceiptSummaryDto(int Id, string ReceiptNumber, DateTime ReceiptDate, string SupplierName, string WarehouseName, int ItemCount);

public record InternalIssueSummaryDto(int Id, string IssueNumber, DateTime IssueDate, string WarehouseName, string? Purpose, int ItemCount);

public record SalesReturnSummaryDto(int Id, string ReturnNumber, DateTime ReturnDate, string WarehouseName, string? CustomerReference, int ItemCount);

public record ScrapRecordSummaryDto(int Id, string RecordNumber, DateTime RecordDate, string WarehouseName, string? Reason, int ItemCount);

public record StockTransferSummaryDto(int Id, string TransferNumber, DateTime TransferDate, string SourceWarehouseName, string DestinationWarehouseName, string Status, int ItemCount);

public record StockCountSummaryDto(int Id, string CountNumber, DateTime CountDate, string WarehouseName, string Status);
