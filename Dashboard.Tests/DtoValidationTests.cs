using System.ComponentModel.DataAnnotations;
using Dashboard.Application.DTOs;
using Dashboard.Application.Helpers;

namespace Dashboard.Tests;

/// <summary>
/// تست‌های attribute های اعتبارسنجیِ DTO ها.
/// این‌ها همان قواعدی هستند که Blazor (DataAnnotationsValidator) در فرم‌ها اجرا می‌کند،
/// پس با <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>
/// همان مسیر واقعی را می‌سنجیم.
/// </summary>
public class DtoValidationTests
{
    private static IReadOnlyList<string> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? string.Empty).ToList();
    }

    private static bool IsValid(object model) => Validate(model).Count == 0;

    // ---------------- مشتری / تأمین‌کننده / انبار ----------------

    [Fact]
    public void CreateCustomerDto_RequiresName()
    {
        Assert.False(IsValid(new CreateCustomerDto { Name = string.Empty }));
        Assert.False(IsValid(new CreateCustomerDto { Name = "   " }));
        Assert.True(IsValid(new CreateCustomerDto { Name = "فروشگاه گل ارکیده" }));
    }

    [Fact]
    public void CreateCustomerDto_AcceptsPersianDigitsInPhoneAndRejectsLetters()
    {
        Assert.True(IsValid(new CreateCustomerDto { Name = "مشتری", Phone = "۰۹۱۲۰۰۰۰۰۰۰" }));
        Assert.False(IsValid(new CreateCustomerDto { Name = "مشتری", Phone = "تلفن ندارد" }));
    }

    [Fact]
    public void CreateCustomerDto_RejectsTooLongAddress()
    {
        Assert.False(IsValid(new CreateCustomerDto { Name = "مشتری", Address = new string('ا', 501) }));
    }

    [Fact]
    public void CreateSupplierDto_RequiresName()
    {
        Assert.False(IsValid(new CreateSupplierDto { Name = string.Empty }));
        Assert.True(IsValid(new CreateSupplierDto { Name = "گلخانه مرکزی" }));
    }

    [Fact]
    public void CreateWarehouseDto_RequiresName()
    {
        Assert.False(IsValid(new CreateWarehouseDto { Name = string.Empty }));
        Assert.True(IsValid(new CreateWarehouseDto { Name = "انبار مرکزی", Code = "WH-01" }));
    }

    // ---------------- حسابداری ----------------

    [Fact]
    public void CreateFinancialAccountDto_RequiresNameAndValidType()
    {
        Assert.False(IsValid(new CreateFinancialAccountDto { Name = string.Empty, Type = "Cash" }));
        Assert.False(IsValid(new CreateFinancialAccountDto { Name = "صندوق اصلی", Type = "Wallet" }));
        Assert.True(IsValid(new CreateFinancialAccountDto { Name = "صندوق اصلی", Type = "Cash" }));
        Assert.True(IsValid(new CreateFinancialAccountDto { Name = "بانک ملت", Type = "Bank", BankName = "بانک ملت" }));
    }

    [Fact]
    public void CreateCustomerReceiptDto_RequiresAccountAndPositiveAmount()
    {
        Assert.True(IsValid(new CreateCustomerReceiptDto
        {
            CustomerId = 1,
            FinancialAccountId = 2,
            Amount = 1_000_000m,
            Method = "Cash"
        }));

        // صندوق/بانک انتخاب نشده (مقدار پیش‌فرض صفر)
        Assert.False(IsValid(new CreateCustomerReceiptDto { FinancialAccountId = 0, Amount = 1_000m }));
        // مبلغ نامعتبر
        Assert.False(IsValid(new CreateCustomerReceiptDto { FinancialAccountId = 2, Amount = 0m }));
        // روش دریافت نامعتبر
        Assert.False(IsValid(new CreateCustomerReceiptDto { FinancialAccountId = 2, Amount = 1_000m, Method = "Crypto" }));
    }

    [Fact]
    public void CreateSupplierPaymentDto_RequiresSupplierAccountAndAmount()
    {
        Assert.True(IsValid(new CreateSupplierPaymentDto
        {
            SupplierId = 1,
            FinancialAccountId = 2,
            Amount = 500_000m,
            Method = "BankTransfer"
        }));

        Assert.False(IsValid(new CreateSupplierPaymentDto { SupplierId = 0, FinancialAccountId = 2, Amount = 500m }));
        Assert.False(IsValid(new CreateSupplierPaymentDto { SupplierId = 1, FinancialAccountId = 0, Amount = 500m }));
        Assert.False(IsValid(new CreateSupplierPaymentDto { SupplierId = 1, FinancialAccountId = 2, Amount = 0m }));
    }

    // ---------------- اسناد انبار ----------------

    [Fact]
    public void CreatePurchaseReceiptDto_RequiresSupplierAndWarehouse()
    {
        var items = new List<PurchaseReceiptItemInput>
        {
            new() { ProductId = 5, Quantity = 3, UnitCost = 10_000m }
        };

        Assert.True(IsValid(new CreatePurchaseReceiptDto { SupplierId = 1, WarehouseId = 2, Items = items }));
        Assert.False(IsValid(new CreatePurchaseReceiptDto { SupplierId = 0, WarehouseId = 2, Items = items }));
        Assert.False(IsValid(new CreatePurchaseReceiptDto { SupplierId = 1, WarehouseId = 0, Items = items }));
        Assert.False(IsValid(new CreatePurchaseReceiptDto { SupplierId = 1, WarehouseId = 2, Items = new() }));
    }

    [Fact]
    public void WarehouseDocuments_RequireWarehouse()
    {
        Assert.True(IsValid(new CreateInternalIssueDto
        {
            WarehouseId = 1,
            Items = new() { new StockItemInput { ProductId = 1, Quantity = 1 } }
        }));
        Assert.False(IsValid(new CreateInternalIssueDto { WarehouseId = 0 }));

        Assert.True(IsValid(new CreateSalesReturnDto
        {
            WarehouseId = 1,
            Items = new() { new StockItemInput { ProductId = 1, Quantity = 1 } }
        }));
        Assert.False(IsValid(new CreateSalesReturnDto { WarehouseId = 0 }));

        Assert.True(IsValid(new CreateScrapRecordDto
        {
            WarehouseId = 1,
            Items = new() { new StockItemInput { ProductId = 1, Quantity = 1 } }
        }));
        Assert.False(IsValid(new CreateScrapRecordDto { WarehouseId = 0 }));
    }

    [Fact]
    public void CreateStockTransferDto_RequiresBothWarehouses()
    {
        var items = new List<StockItemInput> { new() { ProductId = 1, Quantity = 1 } };

        Assert.True(IsValid(new CreateStockTransferDto { SourceWarehouseId = 1, DestinationWarehouseId = 2, Items = items }));
        Assert.False(IsValid(new CreateStockTransferDto { SourceWarehouseId = 0, DestinationWarehouseId = 2, Items = items }));
        Assert.False(IsValid(new CreateStockTransferDto { SourceWarehouseId = 1, DestinationWarehouseId = 0, Items = items }));
    }

    [Fact]
    public void StockDocuments_ReportInvalidItemRows()
    {
        var dto = new CreateStockTransferDto
        {
            SourceWarehouseId = 1,
            DestinationWarehouseId = 2,
            Items = new()
            {
                new StockItemInput { ProductId = 0, Quantity = 0 }, // ردیف اول ناقص
                new StockItemInput { ProductId = 7, Quantity = 2 }  // ردیف دوم درست
            }
        };

        var messages = Validate(dto);
        Assert.Contains(messages, m => m.Contains("کالای ردیف ۱"));
        Assert.Contains(messages, m => m.Contains("مقدار ردیف ۱"));
        Assert.DoesNotContain(messages, m => m.Contains("ردیف ۲"));
    }

    [Fact]
    public void StockDocuments_RequireAtLeastOneItem()
    {
        var dto = new CreateScrapRecordDto { WarehouseId = 1, Items = new() };
        Assert.Contains(Validate(dto), m => m.Contains("حداقل یک قلم کالا"));
    }

    // ---------------- فاکتور فروش ----------------

    [Fact]
    public void CreateSalesInvoiceDto_RequiresWarehouseAndValidItems()
    {
        Assert.True(IsValid(new CreateSalesInvoiceDto
        {
            WarehouseId = 1,
            Items = new() { new SalesInvoiceItemInput { ProductId = 1, Quantity = 2, UnitPrice = 100 } }
        }));

        Assert.False(IsValid(new CreateSalesInvoiceDto
        {
            WarehouseId = 0,
            Items = new() { new SalesInvoiceItemInput { ProductId = 1, Quantity = 2, UnitPrice = 100 } }
        }));

        // تخفیف منفی
        Assert.False(IsValid(new CreateSalesInvoiceDto
        {
            WarehouseId = 1,
            DiscountAmount = -1,
            Items = new() { new SalesInvoiceItemInput { ProductId = 1, Quantity = 2, UnitPrice = 100 } }
        }));

        // اقلام خالی
        Assert.Contains(
            Validate(new CreateSalesInvoiceDto { WarehouseId = 1, Items = new() }),
            m => m.Contains("حداقل یک قلم کالا"));
    }

    // ---------------- کاتالوگ ----------------

    [Fact]
    public void CreateCategoryDto_RequiresNameAndSlugFormat()
    {
        Assert.True(IsValid(new CreateCategoryDto { Name = "گل‌های آپارتمانی", Slug = "indoor-plants" }));
        Assert.False(IsValid(new CreateCategoryDto { Name = string.Empty, Slug = "indoor-plants" }));
        Assert.False(IsValid(new CreateCategoryDto { Name = "گل", Slug = string.Empty }));
        Assert.False(IsValid(new CreateCategoryDto { Name = "گل", Slug = "Indoor Plants" }));
    }

    [Fact]
    public void CreateUnitDto_RequiresName()
    {
        Assert.False(IsValid(new CreateUnitDto { Name = string.Empty }));
        Assert.True(IsValid(new CreateUnitDto { Name = "کیلوگرم" }));
    }

    [Fact]
    public void CreateProductGroupDto_RequiresNameAndSlug()
    {
        Assert.True(IsValid(new CreateProductGroupDto { Name = "تی‌شرت مردانه", Slug = "mens-tshirt" }));
        Assert.False(IsValid(new CreateProductGroupDto { Name = string.Empty, Slug = "mens-tshirt" }));
        Assert.False(IsValid(new CreateProductGroupDto { Name = "تی‌شرت", Slug = string.Empty }));
    }

    [Fact]
    public void CreateVariantDto_RequiresSkuNameAndPositivePrice()
    {
        Assert.True(IsValid(new CreateVariantDto { Sku = "SKU-1", VariantName = "قرمز - L", Price = 100_000m }));
        Assert.False(IsValid(new CreateVariantDto { Sku = string.Empty, VariantName = "قرمز - L", Price = 100_000m }));
        Assert.False(IsValid(new CreateVariantDto { Sku = "SKU-1", VariantName = string.Empty, Price = 100_000m }));
        Assert.False(IsValid(new CreateVariantDto { Sku = "SKU-1", VariantName = "قرمز - L", Price = 0m }));
    }

    // ---------------- اقساط ----------------

    [Fact]
    public void CreateInstallmentPlanDto_RequiresInvoiceAndAtLeastOneInstallment()
    {
        Assert.True(IsValid(new CreateInstallmentPlanDto
        {
            SalesInvoiceId = 1,
            Installments = new() { new InstallmentInput(DateTime.UtcNow.AddMonths(1), 100_000m) }
        }));
        Assert.False(IsValid(new CreateInstallmentPlanDto { SalesInvoiceId = 0, Installments = new() }));
        Assert.False(IsValid(new CreateInstallmentPlanDto { SalesInvoiceId = 1, Installments = new() }));
    }

    // ---------------- ستاره‌ی «الزامی» ----------------

    [Fact]
    public void ValidationAttributeInspector_DetectsRequiredFromDtoAttributes()
    {
        var customer = new CreateCustomerDto();
        Assert.True(ValidationAttributeInspector.IsRequired(() => customer.Name));
        Assert.False(ValidationAttributeInspector.IsRequired(() => customer.Phone));

        var invoice = new CreateSalesInvoiceDto();
        Assert.True(ValidationAttributeInspector.IsRequired(() => invoice.WarehouseId));     // Range(1, …)
        Assert.False(ValidationAttributeInspector.IsRequired(() => invoice.DiscountAmount)); // Range(0, …)
        Assert.False(ValidationAttributeInspector.IsRequired(() => invoice.CustomerId));     // اختیاری
    }

    [Fact]
    public void ValidationAttributeInspector_ReturnsFalseForMissingExpression()
    {
        Assert.False(ValidationAttributeInspector.IsRequired(null));
    }
}
