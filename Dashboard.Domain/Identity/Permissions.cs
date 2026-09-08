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

    public static readonly string[] All =
    {
        ProductsView, ProductsManage, AuditLogsView,
        WarehousesManage, SuppliersManage, StockView,
        PurchaseReceiptsManage, InternalIssuesManage, SalesReturnsManage,
        ScrapRecordsManage, StockTransfersManage, StockCountsManage
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
        _ => permission
    };
}
