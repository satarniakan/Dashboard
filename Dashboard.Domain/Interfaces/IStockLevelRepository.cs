using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IStockLevelRepository
{
    Task<StockLevel?> GetAsync(int productId, int warehouseId);
    Task<IEnumerable<StockLevel>> GetByWarehouseAsync(int warehouseId);
    Task<IEnumerable<StockLevel>> GetByProductAsync(int productId);
    Task<IEnumerable<StockLevel>> GetAllAsync();

    /// کالاهایی که موجودی‌شان از نقطه‌ی سفارش کمتر شده (در کل انبارها)
    Task<IEnumerable<StockLevel>> GetBelowReorderPointAsync();

    /// اگر رکورد (کالا، انبار) وجود نداشت می‌سازد، در غیر این صورت مقدار را می‌افزاید (می‌تواند منفی باشد)
    Task IncreaseOrCreateAsync(int productId, int warehouseId, decimal delta);

    /// <summary>
    /// کاهش موجودی با بررسی کفایت — اگر موجودی کافی نباشد استثنا پرتاب می‌شود.
    /// این متد به همراه RowVersion (همزمانی خوش‌بینانه) از race condition بین چند درخواست همزمان جلوگیری می‌کند.
    /// </summary>
    Task DecreaseWithCheckAsync(int productId, int warehouseId, decimal quantity);

    /// جمع موجودی هر کالا در کل انبارها — برای هشدار نقطه‌ی سفارش (نه ویترین فروشگاه؛
    /// ویترین و سبد فقط موجودی انبار فروشگاه را می‌بینند تا با کسر واقعی سازگار باشد)
    Task<Dictionary<int, decimal>> GetTotalStockAsync(IReadOnlyCollection<int> productIds);

    /// موجودی هر کالا فقط در یک انبار مشخص — سفارش‌های فروشگاه از همین انبار کسر می‌شوند،
    /// پس بررسی کفایت هم باید روی همان انبار باشد (نه جمع همه انبارها)
    Task<Dictionary<int, decimal>> GetWarehouseStockAsync(IReadOnlyCollection<int> productIds, int warehouseId);

    /// <summary>موجودی قابل فروش = موجودی فیزیکی − مقدار رزروشدهٔ سفارش‌های پرداخت‌نشده</summary>
    Task<Dictionary<int, decimal>> GetAvailableForSaleAsync(IReadOnlyCollection<int> productIds, int warehouseId);

    /// <summary>
    /// رزرو اتمیک موجودی برای یک سفارش (یک UPDATE شرطی در دیتابیس).
    /// فقط وقتی موفق است که «موجودی − رزرو» برای این مقدار کافی باشد؛
    /// false یعنی در همین لحظه کسی زودتر رزرو کرده است (برای پیام خطای کاربر).
    /// </summary>
    Task<bool> TryReserveAsync(int productId, int warehouseId, decimal quantity);

    /// <summary>
    /// آزادکردن رزرو — هرگز موجودی رزرو را منفی نمی‌کند و اگر رزروی نباشد بی‌اثر است
    /// (پس فراخوانی دوباره هم مشکلی ایجاد نمی‌کند).
    /// </summary>
    Task ReleaseReservationAsync(int productId, int warehouseId, decimal quantity);
}
