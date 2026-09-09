using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface IInstallmentService
{
    Task CreateInstallmentPlanAsync(CreateInstallmentPlanDto dto, string? userId);
    Task<InstallmentPlanDto?> GetInstallmentPlanAsync(int salesInvoiceId);
    Task<IEnumerable<OverdueInstallmentDto>> GetOverdueInstallmentsAsync();
    Task<IEnumerable<InstallmentDto>> GetAllInstallmentsAsync();
}

public class InstallmentService : IInstallmentService
{
    private readonly IUnitOfWork _unitOfWork;

    public InstallmentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task CreateInstallmentPlanAsync(CreateInstallmentPlanDto dto, string? userId)
    {
        if (dto.Installments.Count == 0)
            throw new InvalidOperationException("حداقل یک قسط لازم است.");

        var invoice = await _unitOfWork.SalesInvoices.GetByIdAsync(dto.SalesInvoiceId)
            ?? throw new InvalidOperationException("فاکتور یافت نشد.");

        if (invoice.Status != Domain.Enums.SalesInvoiceStatus.Confirmed)
            throw new InvalidOperationException("فقط برای فاکتور تأییدشده می‌توان طرح اقساط تعریف کرد.");

        var existing = await _unitOfWork.InstallmentPlans.GetBySalesInvoiceIdAsync(dto.SalesInvoiceId);
        if (existing is not null)
            throw new InvalidOperationException("برای این فاکتور قبلاً طرح اقساط تعریف شده است.");

        var sumInstallments = dto.Installments.Sum(i => i.Amount);
        if (sumInstallments != invoice.TotalAmount)
            throw new InvalidOperationException(
                $"جمع اقساط ({sumInstallments}) باید دقیقاً برابر مبلغ فاکتور ({invoice.TotalAmount}) باشد.");

        var plan = new InstallmentPlan
        {
            SalesInvoiceId = dto.SalesInvoiceId,
            CreatedByUserId = userId
        };

        var seq = 1;
        foreach (var input in dto.Installments.OrderBy(i => i.DueDate))
        {
            plan.Installments.Add(new Installment
            {
                SequenceNumber = seq++,
                DueDate = input.DueDate,
                Amount = input.Amount
            });
        }

        await _unitOfWork.InstallmentPlans.AddAsync(plan);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("InstallmentPlanCreated", userId,
            $"طرح اقساط برای فاکتور {invoice.InvoiceNumber} با {dto.Installments.Count} قسط ثبت شد."));
        await _unitOfWork.CompleteAsync();
    }

    public async Task<InstallmentPlanDto?> GetInstallmentPlanAsync(int salesInvoiceId)
    {
        var plan = await _unitOfWork.InstallmentPlans.GetBySalesInvoiceIdAsync(salesInvoiceId);
        if (plan is null) return null;

        return new InstallmentPlanDto(
            plan.Id, plan.SalesInvoiceId, plan.SalesInvoice!.InvoiceNumber, plan.SalesInvoice.Customer?.Name,
            plan.SalesInvoice.TotalAmount,
            plan.Installments.OrderBy(i => i.SequenceNumber)
                .Select(i => new InstallmentDto(i.Id, i.SequenceNumber, i.DueDate, i.Amount, i.PaidAmount, i.IsFullyPaid, i.IsOverdue))
                .ToList());
    }

    public async Task<IEnumerable<OverdueInstallmentDto>> GetOverdueInstallmentsAsync()
    {
        var overdue = await _unitOfWork.InstallmentPlans.GetOverdueInstallmentsAsync();

        return overdue.Select(i => new OverdueInstallmentDto(
            i.Id,
            i.InstallmentPlan!.SalesInvoice!.InvoiceNumber,
            i.InstallmentPlan.SalesInvoice.Customer?.Name ?? "-",
            i.DueDate,
            i.Amount,
            i.PaidAmount,
            (DateTime.UtcNow.Date - i.DueDate.Date).Days));
    }

    public async Task<IEnumerable<InstallmentDto>> GetAllInstallmentsAsync()
    {
        var all = await _unitOfWork.InstallmentPlans.GetAllInstallmentsAsync();
        return all.Select(i => new InstallmentDto(i.Id, i.SequenceNumber, i.DueDate, i.Amount, i.PaidAmount, i.IsFullyPaid, i.IsOverdue));
    }
}