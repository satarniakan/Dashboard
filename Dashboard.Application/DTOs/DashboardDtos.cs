namespace Dashboard.Application.DTOs;

public record DashboardKpiDto(
    decimal TodaySales,
    decimal MonthSales,
    decimal MonthProfit,
    int TodayInvoiceCount,
    decimal TotalReceivable,
    int OverdueInstallmentCount,
    decimal OverdueAmount,
    int LowStockCount);

public record SalesTrendPointDto(DateTime Date, decimal TotalAmount);

public record TopProductDto(string ProductName, decimal TotalQuantitySold, decimal TotalRevenue);

public record RecentInvoiceDto(int Id, string InvoiceNumber, DateTime InvoiceDate, string? CustomerName, decimal TotalAmount, string Status);

public record LowStockItemDto(string ProductName, string WarehouseName, decimal QuantityOnHand, int ReorderPoint);

public record DashboardDataDto(
    DashboardKpiDto Kpis,
    List<SalesTrendPointDto> SalesTrend,
    List<TopProductDto> TopProducts,
    List<RecentInvoiceDto> RecentInvoices,
    List<LowStockItemDto> LowStockItems);