using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;
using Dashboard.Application.Helpers;
using Dashboard.Domain.Accounting;
namespace Dashboard.Application.Services;

public interface IDashboardService
{
    Task<DashboardDataDto> GetDashboardDataAsync();
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJournalService _journalService;

    public DashboardService(IUnitOfWork unitOfWork, IJournalService journalService)
    {
        _unitOfWork = unitOfWork;
        _journalService = journalService;
    }

    public async Task<DashboardDataDto> GetDashboardDataAsync()
    {
        // «امروز» و «ابتدای ماه» بر مبنای روز تهرانی محاسبه می‌شوند، نه UTC؛ وگرنه در بازهٔ
        // ۰۰:۰۰ تا ۰۳:۳۰ تهران (که UTC هنوز روز قبل است) آمار یک روز عقب می‌ماند و با تاریخ
        // شمسی‌ای که کاربر در جدول می‌بیند نمی‌خواند.
        var tehranNow = PersianDateHelper.ToTehran(DateTime.UtcNow);
        var today = tehranNow.Date;
        var monthStart = new DateTime(tehranNow.Year, tehranNow.Month, 1);

        // ⚠️ فقط فاکتور «تأییدشده» فروش واقعی است. پیش‌نویس هنوز فروشی نیست (ممکن است
        // هرگز تأیید نشود) و اگر در آمار بیاید، «فروش امروز» و نمودار روند و «کالای
        // پرفروش» عددی را نشان می‌دهند که هرگز واقعی نشده است. لغوشده هم که قبلاً
        // کنار گذاشته شده بود.
        var allInvoices = (await _unitOfWork.SalesInvoices.GetAllAsync())
            .Where(i => i.Status == SalesInvoiceStatus.Confirmed)
            .ToList();

        var todayInvoices = allInvoices.Where(i => i.InvoiceDate.ToTehranDate() == today).ToList();
        var monthInvoices = allInvoices.Where(i => i.InvoiceDate.ToTehranDate() >= monthStart).ToList();

        var todaySales = todayInvoices.Sum(i => i.TotalAmount);
        var monthSales = monthInvoices.Sum(i => i.TotalAmount);

        // «to» اینیسیالو «تا پایان همان روز» فهمیده می‌شود (سرویس خودش روز بعد می‌سازد)،
        // پس دیگر لازم نیست AddTicks(-1) دست‌کاری شود.
        var pnl = await _journalService.GetProfitAndLossAsync(monthStart, today);

        var overdue = await _unitOfWork.InstallmentPlans.GetOverdueInstallmentsAsync();
        var overdueList = overdue.ToList();

        var allStock = await GetAllStockLevelsWithLowFlagAsync();

        var kpis = new DashboardKpiDto(
            TodaySales: todaySales,
            MonthSales: monthSales,
            MonthProfit: pnl.NetProfit,
            TodayInvoiceCount: todayInvoices.Count,
            TotalReceivable: await GetTotalReceivableAsync(),
            OverdueInstallmentCount: overdueList.Count,
            OverdueAmount: overdueList.Sum(o => o.Amount - o.PaidAmount),
            LowStockCount: allStock.Count);

        var salesTrend = BuildSalesTrend(allInvoices, days: 7);

        var topProducts = allInvoices
            .SelectMany(i => i.Items)
            .GroupBy(item => item.Product?.Name ?? "-")
            .Select(g => new TopProductDto(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .OrderByDescending(p => p.TotalRevenue)
            .Take(5)
            .ToList();

        var recentInvoices = allInvoices
            .OrderByDescending(i => i.InvoiceDate)
            .Take(5)
            .Select(i => new RecentInvoiceDto(i.Id, i.InvoiceNumber, i.InvoiceDate, i.Customer?.Name, i.TotalAmount, i.Status.ToString()))
            .ToList();

        return new DashboardDataDto(kpis, salesTrend, topProducts, recentInvoices, allStock);
    }

    private static List<SalesTrendPointDto> BuildSalesTrend(List<Domain.Entities.SalesInvoice> invoices, int days)
    {
        var result = new List<SalesTrendPointDto>();
        var today = PersianDateHelper.ToTehran(DateTime.UtcNow).Date; // روز تهرانی، هم‌راستا با KPIهای بالا

        for (var i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var total = invoices.Where(inv => inv.InvoiceDate.ToTehranDate() == day).Sum(inv => inv.TotalAmount);
            result.Add(new SalesTrendPointDto(day, total));
        }

        return result;
    }

    private async Task<decimal> GetTotalReceivableAsync() =>
        await _unitOfWork.JournalEntries.GetAccountNetBalanceAsync(SystemAccountCodes.AccountsReceivable);

    private async Task<List<LowStockItemDto>> GetAllStockLevelsWithLowFlagAsync()
    {
        var warehouses = await _unitOfWork.Warehouses.GetAllAsync();
        var result = new List<LowStockItemDto>();

        foreach (var warehouse in warehouses)
        {
            var levels = await _unitOfWork.StockLevels.GetByWarehouseAsync(warehouse.Id);
            foreach (var level in levels)
            {
                if (level.Product is not null && level.Product.ReorderPoint > 0 && level.QuantityOnHand <= level.Product.ReorderPoint)
                {
                    result.Add(new LowStockItemDto(level.Product.Name, warehouse.Name, level.QuantityOnHand, level.Product.ReorderPoint));
                }
            }
        }

        return result;
    }
}