using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

// دفتر تغییرناپذیر همه رویدادهای انبار — هیچ‌وقت Update/Delete نمی‌شود
public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }

    // مثبت = ورود به انبار، منفی = خروج از انبار
    public int Quantity { get; set; }

    public StockMovementReason Reason { get; set; }

    // اشاره به سند مرتبط (مثلاً شماره فاکتور فروش)
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    public string? Note { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}