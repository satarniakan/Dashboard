using Dashboard.Domain.Enums;

namespace Dashboard.Domain.Entities;

/// <summary>سرفصل حساب در دفتر حسابداری (مثل «صندوق»، «فروش کالا»)</summary>
public class Account
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }

    public int? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }

    // حساب‌های سیستمی (مثل «فروش کالا») را نمی‌توان حذف کرد چون کد به آن‌ها ارجاع می‌دهد
    public bool IsSystemAccount { get; set; }
    public bool IsActive { get; set; } = true;
}