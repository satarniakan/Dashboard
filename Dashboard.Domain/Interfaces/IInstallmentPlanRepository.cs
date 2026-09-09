using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface IInstallmentPlanRepository
{
    Task<InstallmentPlan?> GetBySalesInvoiceIdAsync(int salesInvoiceId);
    Task<Installment?> GetInstallmentByIdAsync(int installmentId);
    Task AddAsync(InstallmentPlan plan);
    Task<IEnumerable<Installment>> GetOverdueInstallmentsAsync();
    Task<IEnumerable<Installment>> GetAllInstallmentsAsync();
}