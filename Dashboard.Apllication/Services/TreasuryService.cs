using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface ITreasuryService
{
    Task<int> CreateFinancialAccountAsync(CreateFinancialAccountDto dto);
    Task<IEnumerable<FinancialAccountDto>> GetFinancialAccountsAsync();

    Task<int> RegisterCustomerReceiptAsync(CreateCustomerReceiptDto dto, string? userId);
    Task<IEnumerable<CustomerReceiptDto>> GetCustomerReceiptsAsync();

    Task<int> RegisterSupplierPaymentAsync(CreateSupplierPaymentDto dto, string? userId);
    Task<IEnumerable<SupplierPaymentDto>> GetSupplierPaymentsAsync();
}

public class TreasuryService : ITreasuryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJournalService _journalService;

    public TreasuryService(IUnitOfWork unitOfWork, IJournalService journalService)
    {
        _unitOfWork = unitOfWork;
        _journalService = journalService;
    }

    // هر صندوق/بانک جدید، خودش هم یه سرفصل حساب معادل تو دفتر کل می‌سازه
    public async Task<int> CreateFinancialAccountAsync(CreateFinancialAccountDto dto)
    {
        var type = Enum.Parse<FinancialAccountType>(dto.Type);
        var accountCode = type == FinancialAccountType.Cash ? $"1101-{Guid.NewGuid().ToString()[..4]}" : $"1102-{Guid.NewGuid().ToString()[..4]}";

        var ledgerAccount = new Account
        {
            Code = accountCode,
            Name = dto.Name,
            Type = Domain.Enums.AccountType.Asset,
            IsSystemAccount = true
        };
        await _unitOfWork.Accounts.AddAsync(ledgerAccount);

        var financialAccount = new FinancialAccount
        {
            Name = dto.Name,
            Type = type,
            BankName = dto.BankName,
            AccountNumber = dto.AccountNumber,
            Iban = dto.Iban,
            Account = ledgerAccount
        };
        await _unitOfWork.FinancialAccounts.AddAsync(financialAccount);
        await _unitOfWork.CompleteAsync();

        return financialAccount.Id;
    }

    public async Task<IEnumerable<FinancialAccountDto>> GetFinancialAccountsAsync()
    {
        var accounts = await _unitOfWork.FinancialAccounts.GetAllAsync();
        return accounts.Select(a => new FinancialAccountDto(a.Id, a.Name, a.Type.ToString()));
    }

    // ---------------- دریافت از مشتری ----------------

    public async Task<int> RegisterCustomerReceiptAsync(CreateCustomerReceiptDto dto, string? userId)
    {
        if (dto.Amount <= 0) throw new InvalidOperationException("مبلغ باید بزرگتر از صفر باشد.");

        var financialAccount = await _unitOfWork.FinancialAccounts.GetByIdAsync(dto.FinancialAccountId)
            ?? throw new InvalidOperationException("صندوق/بانک انتخاب‌شده یافت نشد.");

        var receipt = new CustomerReceipt
        {
            CustomerId = dto.CustomerId,
            FinancialAccountId = dto.FinancialAccountId,
            ReceiptNumber = dto.ReceiptNumber,
            Amount = dto.Amount,
            Method = Enum.Parse<PaymentMethod>(dto.Method),
            ReceiptDate = dto.ReceiptDate,
            ChequeNumber = dto.ChequeNumber,
            ChequeDueDate = dto.ChequeDueDate,
            Notes = dto.Notes,
            CreatedByUserId = userId
        };

        await _unitOfWork.CustomerReceipts.AddAsync(receipt);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("CustomerReceiptRegistered", userId, $"دریافت {dto.ReceiptNumber} به مبلغ {dto.Amount} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        // دریافت پول: بدهکار صندوق/بانک، بستانکار حساب‌های دریافتنی (طلب از مشتری کم می‌شود)
        await _journalService.PostEntryAsync(
            description: $"دریافت وجه طبق رسید {dto.ReceiptNumber}",
            lines: new List<JournalLineInput>
            {
                new(financialAccount.Account!.Code, dto.Amount, 0, "افزایش موجودی صندوق/بانک"),
                new("1200", 0, dto.Amount, "کاهش طلب از مشتری")
            },
            referenceType: nameof(CustomerReceipt),
            referenceId: receipt.Id,
            userId: userId);

        return receipt.Id;
    }

    public async Task<IEnumerable<CustomerReceiptDto>> GetCustomerReceiptsAsync()
    {
        var receipts = await _unitOfWork.CustomerReceipts.GetAllAsync();
        return receipts.Select(r => new CustomerReceiptDto(
            r.Id, r.ReceiptNumber, r.ReceiptDate, r.Customer?.Name, r.FinancialAccount?.Name ?? "-", r.Amount, r.Method.ToString()));
    }

    // ---------------- پرداخت به تأمین‌کننده ----------------

    public async Task<int> RegisterSupplierPaymentAsync(CreateSupplierPaymentDto dto, string? userId)
    {
        if (dto.Amount <= 0) throw new InvalidOperationException("مبلغ باید بزرگتر از صفر باشد.");

        var financialAccount = await _unitOfWork.FinancialAccounts.GetByIdAsync(dto.FinancialAccountId)
            ?? throw new InvalidOperationException("صندوق/بانک انتخاب‌شده یافت نشد.");

        var payment = new SupplierPayment
        {
            SupplierId = dto.SupplierId,
            FinancialAccountId = dto.FinancialAccountId,
            PaymentNumber = dto.PaymentNumber,
            Amount = dto.Amount,
            Method = Enum.Parse<PaymentMethod>(dto.Method),
            PaymentDate = dto.PaymentDate,
            ChequeNumber = dto.ChequeNumber,
            ChequeDueDate = dto.ChequeDueDate,
            Notes = dto.Notes,
            CreatedByUserId = userId
        };

        await _unitOfWork.SupplierPayments.AddAsync(payment);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SupplierPaymentRegistered", userId, $"پرداخت {dto.PaymentNumber} به مبلغ {dto.Amount} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        // پرداخت پول: بدهکار حساب‌های پرداختنی (بدهی کم می‌شود)، بستانکار صندوق/بانک
        await _journalService.PostEntryAsync(
            description: $"پرداخت وجه طبق سند {dto.PaymentNumber}",
            lines: new List<JournalLineInput>
            {
                new("2100", dto.Amount, 0, "کاهش بدهی به تأمین‌کننده", "Supplier", dto.SupplierId),
                new(financialAccount.Account!.Code, 0, dto.Amount, "کاهش موجودی صندوق/بانک")
            },
            referenceType: nameof(SupplierPayment),
            referenceId: payment.Id,
            userId: userId);

        return payment.Id;
    }

    public async Task<IEnumerable<SupplierPaymentDto>> GetSupplierPaymentsAsync()
    {
        var payments = await _unitOfWork.SupplierPayments.GetAllAsync();
        return payments.Select(p => new SupplierPaymentDto(
            p.Id, p.PaymentNumber, p.PaymentDate, p.Supplier?.Name ?? "-", p.FinancialAccount?.Name ?? "-", p.Amount, p.Method.ToString()));
    }
}