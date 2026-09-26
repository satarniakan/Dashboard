using System.ComponentModel.DataAnnotations;

namespace Dashboard.Application.DTOs;

public class SalesInvoiceItemInput
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب کالا الزامی است.")]
    public int ProductId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "مقدار باید بزرگتر از صفر باشد.")]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "قیمت واحد نمی‌تواند منفی باشد.")]
    public decimal UnitPrice { get; set; }
}

public class CreateSalesInvoiceDto : IValidatableObject
{
    public int? CustomerId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب انبار الزامی است.")]
    public int WarehouseId { get; set; }

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    [Range(0, double.MaxValue, ErrorMessage = "تخفیف نمی‌تواند منفی باشد.")]
    public decimal DiscountAmount { get; set; }

    /// <summary>هزینهٔ حمل‌ونقل — در سفارش فروشگاه اینترنتی از Order.ShippingCost می‌آید</summary>
    [Range(0, double.MaxValue, ErrorMessage = "هزینهٔ حمل‌ونقل نمی‌تواند منفی باشد.")]
    public decimal ShippingAmount { get; set; }

    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }

    public int? SalesInvoiceId { get; set; }

    [MinLength(1, ErrorMessage = "حداقل یک قلم کالا لازم است.")]
    public List<SalesInvoiceItemInput> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => StockItemValidation.Validate(Items?.Select(i => (i.ProductId, i.Quantity)));
}

public record SalesInvoiceItemDto(int ProductId, string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record SalesInvoiceDto(
    int Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string? CustomerName,
    string WarehouseName,
    int WarehouseId,
    int? CustomerId,
    string Status,
    decimal DiscountAmount,
    decimal ShippingAmount,
    decimal TotalAmount,
    string? Notes,
    List<SalesInvoiceItemDto> Items);

public record SalesInvoiceSummaryDto(
    int Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string? CustomerName,
    string WarehouseName,
    string Status,
    decimal TotalAmount);