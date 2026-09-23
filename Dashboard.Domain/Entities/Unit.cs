namespace Dashboard.Domain.Entities;

/// <summary>واحد شمارش کالا (مثل «عدد»، «کیلوگرم»، «بسته») — فهرستِ قابل مدیریت که فرم‌های کالا از آن استفاده می‌کنند</summary>
public class Unit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
