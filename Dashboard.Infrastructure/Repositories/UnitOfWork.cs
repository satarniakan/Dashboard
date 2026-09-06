using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data; // این مسیر را بر اساس پروژه خود چک کنید (جایی که AppDbContext در آن است)

namespace Dashboard.Infrastructure.Repositories; // یا هر نیم‌اسپیسی که سایر Repositoryها در آن هستند

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    // در اینجا، مخازن (Repositoryها) را تعریف می‌کنیم
    public IProductRepository Products { get; private set; }
    public IOtpRepository OtpCodes { get; private set; }
    public IAuditLogRepository AuditLogs { get; private set; }

    public UnitOfWork(AppDbContext context,
                      IProductRepository products,
                      IOtpRepository otpCodes,
                      IAuditLogRepository auditLogs)
    {
        _context = context;
        Products = products;
        OtpCodes = otpCodes;
        AuditLogs = auditLogs;
    }

    // این همان متد جادویی است که همه چیز را یک‌باره ذخیره می‌کند
    public async Task<int> CompleteAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
