namespace Dashboard.Domain.Identity;

public static class Permissions
{
    public const string ClaimType = "Permission";

    public const string ProductsView = "products.view";
    public const string ProductsManage = "products.manage";
    public const string AuditLogsView = "auditlogs.view";

    // --- ماژول انبارداری (WMS) ---
    public const string WarehousesManage = "warehouses.manage";
    public const string SuppliersManage = "suppliers.manage";
    public const string StockView = "stock.view";
    public const string PurchaseReceiptsManage = "purchase.receipts.manage";
    public const string InternalIssuesManage = "internal.issues.manage";
    public const string SalesReturnsManage = "sales.returns.manage";
    public const string ScrapRecordsManage = "scrap.records.manage";
    public const string StockTransfersManage = "stock.transfers.manage";
    public const string StockCountsManage = "stock.counts.manage";

    // --- ماژول فروش ---
    public const string CustomersManage = "customers.manage";
    public const string SalesCreate = "sales.create";
    public const string SalesConfirm = "sales.confirm";
    public const string SalesCancel = "sales.cancel";
    public const string SalesView = "sales.view";

    // --- ماژول حسابداری و خزانه‌داری ---
    public const string AccountingView = "accounting.view";
    public const string TreasuryManage = "treasury.manage";
    public const string CatalogManage = "catalog.manage";

    // نکته: AccountingView و TreasuryManage قبلاً بعد از این آرایه تعریف شده بودند،
    // پس داخلش نبودند. نتیجه: نه در صفحه‌ی «مدیریت مجوزها» دیده می‌شدند، نه حتی
    // به نقش Admin به‌صورت خودکار داده می‌شدند (چون RoleSeeder فقط همین آرایه را می‌خواند).
    public static readonly string[] All =
    {
        ProductsView, ProductsManage, AuditLogsView,
        WarehousesManage, SuppliersManage, StockView,
        PurchaseReceiptsManage, InternalIssuesManage, SalesReturnsManage,
        ScrapRecordsManage, StockTransfersManage, StockCountsManage,
        CustomersManage, SalesCreate, SalesConfirm, SalesCancel, SalesView,
        AccountingView, TreasuryManage
    };

    public static string ToPersian(string permission) => permission switch
    {
        ProductsView => "مشاهده محصولات",
        ProductsManage => "افزودن / ویرایش / حذف محصولات",
        AuditLogsView => "مشاهده گزارش رویدادها",
        WarehousesManage => "مدیریت انبارها",
        SuppliersManage => "مدیریت تأمین‌کنندگان",
        StockView => "مشاهده موجودی انبار",
        PurchaseReceiptsManage => "ثبت رسید خرید",
        InternalIssuesManage => "ثبت حواله مصرف داخلی",
        SalesReturnsManage => "ثبت برگشت از فروش",
        ScrapRecordsManage => "ثبت ضایعات",
        StockTransfersManage => "ثبت انتقال بین انبار",
        StockCountsManage => "انجام انبارگردانی",
        CustomersManage => "مدیریت مشتریان",
        SalesCreate => "ثبت فاکتور فروش",
        SalesConfirm => "تأیید فاکتور فروش",
        SalesCancel => "لغو فاکتور فروش",
        SalesView => "مشاهده فاکتورهای فروش",
        AccountingView => "مشاهده حسابداری",
        TreasuryManage => "مدیریت صندوق و بانک",
        CatalogManage => "مدیریت کاتالوگ فروشگاه",
        _ => permission
    };
}
