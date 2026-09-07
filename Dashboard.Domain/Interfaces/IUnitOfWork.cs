using Dashboard.Domain.Interfaces;

namespace Dashboard.Domain.Interfaces;

public interface IUnitOfWork
{
    // این‌ها همان انباردارهایی هستند که سرکارگر باید به آن‌ها دسترسی داشته باشد
    IProductRepository Products { get; }
    IOtpRepository OtpCodes { get; }
    IAuditLogRepository AuditLogs { get; }

    // این همان متد جادویی است که در پایان، همه تغییرات را یک‌باره ذخیره می‌کند
    Task<int> CompleteAsync();
}
